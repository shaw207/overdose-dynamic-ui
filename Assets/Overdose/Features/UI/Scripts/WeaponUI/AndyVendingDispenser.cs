using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AndyVendingDispenser : MonoBehaviour
{
    public enum Stage { Idle, Retracting, Falling, Basin, Intake, Routing }

    [Serializable]
    public sealed class Route
    {
        public Vector3[] points;
        public Transform pipe;
    }

    [SerializeField] private Transform[] products;
    [SerializeField] private AndyVendingProduct[] targets;
    [SerializeField] private int[] occupied;
    [SerializeField] private Route[] routes;
    [SerializeField] private Transform tray;
    [SerializeField] private Transform[] traySurfaces, slotAnchors;
    [SerializeField] private Vector3 outlet;
    [SerializeField] private MedicineItem[] catalogue;
    [SerializeField] private Material runningPipe;

    private const float Step=1f/120f, OutletTime=.24f, ImpactTime=.083f, EjectTime=.46f;
    private const float EquippedHeight=.252f;
    private static readonly Vector3 RearVelocity=new Vector3(0,-.18f,-1.65f);
    private static readonly Vector3 ReleaseVelocity=new Vector3(0,-.57f,-.25f);
    private readonly System.Random random=new System.Random();
    private readonly Transform[] models=new Transform[3];
    private readonly MedicineItem[] displayed=new MedicineItem[3], requested=new MedicineItem[3];
    private Vector3[] home;
    private Quaternion[] facing;
    private Renderer[] pipeRenderers;
    private Material[] finishes;
    private Scene world;
    private PhysicsScene simulation;
    private GameObject physicsRoot;
    private PhysicsMaterial bottleMaterial;
    private Rigidbody body;
    private AndyVendingDeliveryBottle falling;
    private Transform ejected, movingModel;
    private Vector3 departure, routeStart, routeVelocity, rackStart, throat;
    private Quaternion oldFacing, routeFacing;
    private int choice=-1, slot=-1, lit=-1, generation;
    private float elapsed, accumulator, rearTime, intakeTime, tubeTime, ejectAge, finalScale;
    private bool stockRestored;
    private bool open;

    public event Action<int,int> Arrived;
    public event Action Cancelled;
    public Stage Phase { get; private set; }
    public int ActiveCell => choice<0?-1:occupied[choice];
    public int ActiveSlot => slot;
    public int ActiveRouteCount => lit<0?0:1;
    public Rigidbody Body => body;
    public int Contacts => falling?falling.Contacts:0;
    public bool Settled {get;private set;}
    public int CompletedCount {get;private set;}
    public Transform Tray => tray;
    public Transform Ejected => ejected;
    public Transform MovingModel => movingModel;
    public int RouteCount => routes.Length;
    public Transform RoutePipe(int index) => routes[index].pipe;
    public Transform Product(int index) => products[index];
    public AndyVendingProduct Target(int index) => targets[index];
    public Transform SlotModel(int index) => models[index];
    public Transform SlotAnchor(int index) => slotAnchors[index];
    public Vector3 Outlet => transform.TransformPoint(outlet);
    public Vector3 FlowPosition => body?body.position:Outlet;
    public bool HasLocalPhysics => physicsRoot && simulation.IsValid() && simulation!=Physics.defaultPhysicsScene;

    public void SetOpen(bool value)
    {
        if(open==value) return;
        open=value;
        if(!value){ResetMachine();return;}
        if(home==null)
        {
            home=Array.ConvertAll(products,p=>p.localPosition);
            facing=Array.ConvertAll(products,p=>p.localRotation);
            if(slotAnchors!=null && slotAnchors.Length==3 && routes.Length==3)
            {
                pipeRenderers=Array.ConvertAll(routes,r=>r.pipe.GetComponent<Renderer>());
                finishes=Array.ConvertAll(pipeRenderers,r=>r.sharedMaterial);
            }
        }
        foreach(var target in targets) target.SetAvailable(true);
    }

    private void OnDisable()=>SetOpen(false);

    public void ShowSlots(MedicineItem[] equipped)
    {
        if(equipped==null || slotAnchors==null || slotAnchors.Length!=3) return;
        for(int i=0;i<3;i++)
        {
            requested[i]=i<equipped.Length?equipped[i]:null;
            if(i!=slot) RefreshSlot(i);
        }
    }

    private void RefreshSlot(int index)
    {
        var item=requested[index];
        if(displayed[index]==item && (!item || models[index])) return;
        Remove(models[index]);
        models[index]=null;
        displayed[index]=item;
        if(!item || !item.ConsumptionModel) return;
        var mount=new GameObject(item.DisplayName+" equipped").transform;
        mount.SetParent(slotAnchors[index],false);
        GameObject copy=Instantiate(item.ConsumptionModel,mount,false);
        copy.transform.localPosition=Vector3.zero;
        copy.transform.localRotation=Quaternion.Euler(item.ConsumptionModelRotation);
        foreach(var part in copy.GetComponentsInChildren<Transform>(true)) part.gameObject.layer=30;
        var bounds=LocalBounds(mount,copy);
        float scale=EquippedHeight/Mathf.Max(.0001f,bounds.size.y);
        copy.transform.localScale*=scale;
        copy.transform.localPosition=-bounds.center*scale;
        models[index]=mount;
    }

    public bool TryTransfer(int productIndex,int targetSlot)
    {
        if(!open || Phase!=Stage.Idle || productIndex<0 || productIndex>=products.Length || targetSlot<0 || targetSlot>=3 ||
            pipeRenderers==null || catalogue==null || productIndex>=catalogue.Length || !catalogue[productIndex]) return false;
        choice=productIndex;slot=targetSlot;
        Settled=false;
        targets[choice].SetAvailable(false);
        PrepareCarrier();
        Enter(Stage.Retracting);
        return true;
    }

    private void Update()
    {
        if(!open || Phase==Stage.Idle) return;
        // Fixed substeps keep phase crossings, collision callbacks and their visual responses on the same clock.
        accumulator+=Mathf.Min(Time.unscaledDeltaTime,.25f);
        while(accumulator>=Step && Phase!=Stage.Idle)
        {
            accumulator-=Step;
            Advance(Step);
        }
    }

    private void Advance(float dt)
    {
        elapsed+=dt;
        AdvanceEjected(dt);
        switch(Phase)
        {
            case Stage.Retracting:
                MoveToOutlet();return;
            case Stage.Falling:
                simulation.Simulate(dt);
                if(tray.InverseTransformPoint(body.position).y<-.55f || elapsed>8f){Abort();return;}
                if(Contacts==0) return;
                ejected=models[slot];models[slot]=null;displayed[slot]=null;
                if(ejected){departure=ejected.position;oldFacing=ejected.rotation;}
                ejectAge=0f;
                AdvanceEjected(dt);
                Enter(Stage.Basin);return;
            case Stage.Basin:
                simulation.Simulate(dt);
                if(elapsed<ImpactTime) return;
                routeStart=body.position;routeFacing=body.rotation;routeVelocity=body.linearVelocity;
                body.collisionDetectionMode=CollisionDetectionMode.Discrete;
                body.isKinematic=true;
                body.detectCollisions=false;
                Vector3 inlet=transform.TransformPoint(routes[slot].points[0]);
                intakeTime=Mathf.Clamp(Vector3.Distance(routeStart,inlet)/1.35f,.095f,.19f);
                tubeTime=Vector3.Distance(inlet,slotAnchors[slot].position)/1.42f;
                Enter(Stage.Intake);return;
            case Stage.Intake:
                Transfer(true);return;
            case Stage.Routing:
                Transfer(false);return;
        }
    }

    private static Vector3 Curve(Vector3 start,Vector3 end,Vector3 fromVelocity,Vector3 toVelocity,float duration,float t)
    {
        float u=1f-t;
        return (u*u*(1f+2f*t))*start+(t*t*(3f-2f*t))*end+
            duration*(u*u*t*fromVelocity-u*t*t*toVelocity);
    }

    private void MoveToOutlet()
    {
        bool rear=elapsed<rearTime;
        float t=rear?Mathf.Clamp01(elapsed/rearTime):Mathf.Clamp01((elapsed-rearTime)/OutletTime);
        Vector3 p=rear?Curve(rackStart,throat,new Vector3(0,-.11f,2.35f),RearVelocity,rearTime,t):
            Curve(throat,outlet,RearVelocity,ReleaseVelocity,OutletTime,t);
        Quaternion tip=Quaternion.Euler(65f,0,8f), release=Quaternion.Euler(0,0,27f);
        Quaternion q=rear?Quaternion.Slerp(Quaternion.identity,tip,Mathf.SmoothStep(0,1,t)):
            Quaternion.Slerp(tip,release,Mathf.SmoothStep(0,1,t));
        PoseBody(transform.TransformPoint(p),transform.rotation*q);
        if(!stockRestored && elapsed>=rearTime*.55f)
        {
            // Once the original is behind the faceplate, the next unit slides forward into the vacated compartment.
            var next=products[choice];next.gameObject.SetActive(true);
            float fill=Mathf.Clamp01((elapsed-rearTime*.55f)/.18f);
            next.localPosition=home[choice]+Vector3.forward*(.29f*(1f-Mathf.SmoothStep(0,1,fill)));
            if(fill>=1f){Restock(choice);stockRestored=true;}
        }
        if(rear || t<1f) return;
        if(!stockRestored) Restock(choice);
        body.isKinematic=false;body.detectCollisions=true;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        float x=(float)(random.NextDouble()*.22-.11);
        body.linearVelocity=transform.TransformDirection(ReleaseVelocity+Vector3.right*x);
        body.angularVelocity=transform.TransformDirection(new Vector3(2.2f,x*2f,2.6f));
        Enter(Stage.Falling);
    }

    private void AdvanceEjected(float dt)
    {
        if(!ejected) return;
        ejectAge+=dt;
        float t=Mathf.Clamp01(ejectAge/EjectTime);
        ejected.position=departure+transform.TransformVector(new Vector3(.035f*t,-.54f*(.34f*t+.66f*t*t),-.06f*t));
        ejected.rotation=oldFacing*Quaternion.Euler(24f*t,0,-51f*t);
        if(t>=1){Remove(ejected);ejected=null;}
    }

    private void PoseBody(Vector3 position,Quaternion rotation)
    {
        body.transform.SetPositionAndRotation(position,rotation);
        body.position=position;body.rotation=rotation;
    }

    private void Transfer(bool intake)
    {
        Vector3 inlet=transform.TransformPoint(routes[slot].points[0]);
        Vector3 end=slotAnchors[slot].position;
        Vector3 tubeVelocity=(end-inlet).normalized*1.42f;
        float t=Mathf.Clamp01(elapsed/(intake?intakeTime:tubeTime));
        Vector3 p=intake?Curve(routeStart,inlet,routeVelocity,tubeVelocity,intakeTime,t):
            Curve(inlet,end,tubeVelocity,tubeVelocity*.10f,tubeTime,t);
        float progress=(intake?elapsed:intakeTime+elapsed)/(intakeTime+tubeTime);
        Quaternion q=Quaternion.Slerp(routeFacing,slotAnchors[slot].rotation,Mathf.SmoothStep(0,1,progress));
        PoseBody(p,q);
        body.transform.localScale=Vector3.one*Mathf.Lerp(1f,finalScale,progress);
        if(t<1f) return;
        if(intake){SetLit(slot);Enter(Stage.Routing);return;}
        int item=choice, target=slot;
        SetLit(-1);
        var mount=new GameObject(catalogue[item].DisplayName+" equipped").transform;
        mount.SetParent(slotAnchors[target],false);
        movingModel.SetParent(null,true);
        SceneManager.MoveGameObjectToScene(movingModel.gameObject,gameObject.scene);
        movingModel.SetParent(mount,true);
        movingModel=null;
        models[target]=mount;displayed[target]=catalogue[item];
        ClearBody();
        choice=slot=-1;
        Enter(Stage.Idle);
        Settled=true;
        CompletedCount++;
        Arrived?.Invoke(item,target);
        RefreshSlot(target);
    }

    private void Enter(Stage stage){Phase=stage;elapsed=0;}

    private void SetLit(int index)
    {
        if(lit>=0) pipeRenderers[lit].sharedMaterial=finishes[lit];
        lit=index;
        if(index>=0) pipeRenderers[index].sharedMaterial=runningPipe;
    }

    private void EnsureWorld()
    {
        if(physicsRoot) return;
        world=SceneManager.CreateScene("Andy vending physics "+GetEntityId()+"-"+(++generation),
            new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        simulation=world.GetPhysicsScene();
        physicsRoot=new GameObject("Tray collision scene");
        SceneManager.MoveGameObjectToScene(physicsRoot,world);
        foreach(var surface in traySurfaces)
        {
            var proxy=new GameObject(surface.name);
            SceneManager.MoveGameObjectToScene(proxy,world);
            proxy.transform.SetParent(physicsRoot.transform,false);
            proxy.transform.SetPositionAndRotation(surface.position,surface.rotation);
            proxy.AddComponent<BoxCollider>().size=surface.lossyScale;
        }
        bottleMaterial=new PhysicsMaterial("Bottle against catch tray")
        {
            dynamicFriction=.52f,staticFriction=.58f,bounciness=.25f,bounceCombine=PhysicsMaterialCombine.Maximum
        };
    }

    private void PrepareCarrier()
    {
        EnsureWorld();
        movingModel=products[choice];
        var next=Instantiate(movingModel,movingModel.parent,false);
        next.name=movingModel.name;next.gameObject.SetActive(false);
        products[choice]=next;
        Bounds b=LocalBounds(transform,movingModel.gameObject);
        rackStart=b.center;
        throat=new Vector3(outlet.x,outlet.y+.084f,.065f);
        rearTime=Mathf.Clamp(Vector3.Distance(rackStart,throat)/3.35f,.22f,.43f);
        var go=new GameObject("Dispensed "+movingModel.name);
        SceneManager.MoveGameObjectToScene(go,world);
        go.transform.SetParent(physicsRoot.transform,false);
        go.transform.SetPositionAndRotation(transform.TransformPoint(rackStart),transform.rotation);
        movingModel.SetParent(null,true);
        SceneManager.MoveGameObjectToScene(movingModel.gameObject,world);
        movingModel.SetParent(go.transform,true);
        b=LocalBounds(go.transform,movingModel.gameObject);
        finalScale=EquippedHeight/Mathf.Max(.001f,b.size.y);
        var shape=go.AddComponent<CapsuleCollider>();
        shape.height=b.size.y;
        shape.radius=Mathf.Min(shape.height*.48f,Mathf.Max(b.extents.x,b.extents.z)*.91f);
        shape.sharedMaterial=bottleMaterial;
        falling=go.AddComponent<AndyVendingDeliveryBottle>();
        body=go.AddComponent<Rigidbody>();
        body.mass=.15f;body.linearDamping=.25f;body.angularDamping=1.3f;
        body.isKinematic=true;body.detectCollisions=false;
        stockRestored=false;accumulator=0;
    }

    private static Bounds LocalBounds(Transform parent,GameObject model)
    {
        Bounds result=default;bool first=true;
        foreach(var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var b=r.localBounds;var m=parent.worldToLocalMatrix*r.localToWorldMatrix;
            for(int i=0;i<8;i++)
            {
                var p=m.MultiplyPoint3x4(new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z));
                if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);
            }
        }
        return result;
    }

    private static void Remove(Transform thing)
    {
        if(!thing) return;
        thing.gameObject.SetActive(false);Destroy(thing.gameObject);
    }

    private void Restock(int i)
    {
        products[i].SetLocalPositionAndRotation(home[i],facing[i]);
        products[i].gameObject.SetActive(true);targets[i].SetAvailable(open);
    }

    private void ClearBody()
    {
        if(body) Remove(body.transform);
        body=null;falling=null;movingModel=null;
    }

    private void Abort()
    {
        ResetMachine();
        Cancelled?.Invoke();
    }

    private void ResetMachine()
    {
        SetLit(-1);ClearBody();Remove(ejected);ejected=null;
        if(home!=null) for(int i=0;i<products.Length;i++) Restock(i);
        int restore=slot;
        choice=slot=-1;Settled=false;Enter(Stage.Idle);
        if(restore>=0) RefreshSlot(restore);
        if(physicsRoot)
        {
            physicsRoot.SetActive(false);SceneManager.UnloadSceneAsync(world);physicsRoot=null;
        }
        if(bottleMaterial) Destroy(bottleMaterial);
    }
}

