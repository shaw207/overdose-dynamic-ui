using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AndyStationNavigation : MonoBehaviour
{
    public enum Node { Weapons, Medicine, Deployment }

    [SerializeField] private Camera stationCamera;
    [SerializeField] private TMP_Text currentLabel;
    [SerializeField] private Button leftButton;
    [SerializeField] private TMP_Text leftLabel;
    [SerializeField] private Button rightButton;
    [SerializeField] private TMP_Text rightLabel;
    [SerializeField, Min(.05f)] private float turnSeconds = .55f;

    // Departed fires once when leaving a settled node. Arrived fires after the
    // exact camera pose is restored, including when an in-flight turn reverses.
    public event Action<Node> Departed;
    public event Action<Node> Arrived;

    public Node CurrentNode { get; private set; } = Node.Weapons;
    public bool IsTransitioning { get; private set; }
    public bool IsWeapons => sessionOpen && !IsTransitioning && CurrentNode == Node.Weapons;
    public bool IsMedicine => sessionOpen && !IsTransitioning && CurrentNode == Node.Medicine;

    private bool sessionOpen;
    private bool hasEastPose;
    private Quaternion eastPose;
    private Node destination;
    private Node heading;
    private float progress;
    private float startProgress;
    private float endProgress;
    private float elapsed;
    private float duration;
    private Vector2 titleRest;

    private void Awake()
    {
        CaptureEastPose();
        if (currentLabel != null) titleRest = currentLabel.rectTransform.anchoredPosition;
    }

    private void OnEnable()
    {
        if (leftButton != null) leftButton.onClick.AddListener(GoLeft);
        if (rightButton != null) rightButton.onClick.AddListener(GoRight);
    }

    private void OnDisable()
    {
        if (leftButton != null) leftButton.onClick.RemoveListener(GoLeft);
        if (rightButton != null) rightButton.onClick.RemoveListener(GoRight);
        EndSession();
    }

    private void CaptureEastPose()
    {
        if (hasEastPose || stationCamera == null) return;
        eastPose = stationCamera.transform.localRotation;
        hasEastPose = true;
    }

    public void BeginSession()
    {
        if (sessionOpen) return;
        CaptureEastPose();
        if (!hasEastPose) return;
        sessionOpen = true;
        CurrentNode = destination = Node.Weapons;
        IsTransitioning = false;
        ApplyPose(0f);
        DrawHeading(Node.Weapons);
        Arrived?.Invoke(CurrentNode);
    }

    public void EndSession()
    {
        if (!sessionOpen) return;
        sessionOpen = false;
        IsTransitioning = false;
        CurrentNode = destination = Node.Weapons;
        ApplyPose(0f);
        DrawHeading(Node.Weapons);
    }

    public void GoMedicine() => TurnTo(Node.Medicine);
    public void GoWeapons() => TurnTo(Node.Weapons);

    public void GoDeployment() => TurnTo(Node.Deployment);

    private void GoLeft() => TurnTo(heading == Node.Deployment ? Node.Weapons : Node.Medicine);
    private void GoRight() => TurnTo(heading == Node.Medicine ? Node.Weapons : Node.Deployment);

    private void TurnTo(Node next)
    {
        if (!sessionOpen || stationCamera == null || next == destination) return;
        bool leavingRest = !IsTransitioning;
        destination = next;
        startProgress = progress;
        endProgress = next == Node.Medicine ? 1f : next == Node.Deployment ? -1f : 0f;
        elapsed = 0f;
        duration = Mathf.Max(.05f, turnSeconds * Mathf.Abs(endProgress - startProgress));
        IsTransitioning = true;
        if (leavingRest) Departed?.Invoke(CurrentNode);
    }

    private void Update()
    {
        if (!IsTransitioning) return;
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        ApplyPose(Mathf.SmoothStep(startProgress, endProgress, t));
        Node visibleHeading = progress >= .5f ? Node.Medicine : progress <= -.5f ? Node.Deployment : Node.Weapons;
        if (visibleHeading != heading) DrawHeading(visibleHeading);
        if (t < 1f) return;

        CurrentNode = destination;
        IsTransitioning = false;
        Arrived?.Invoke(CurrentNode);
    }

    private void ApplyPose(float value)
    {
        progress = Mathf.Clamp(value, -1f, 1f);
        if (stationCamera == null || !hasEastPose) return;
        // Rotating about the station's up axis keeps the existing camera pitch
        // and position. From the east-facing bench, north lies to the left.
        stationCamera.transform.localRotation = Quaternion.AngleAxis(-90f * progress, Vector3.up) * eastPose;
        // As north enters from the left, let its title settle into the same center as the camera.
        float across = (progress - Mathf.Round(progress)) * 2f;
        float opacity = 1f - Mathf.Abs(across);
        if (currentLabel != null)
        {
            currentLabel.rectTransform.anchoredPosition = titleRest + Vector2.right * (across * 28f);
            currentLabel.alpha = opacity;
        }
        if (leftLabel != null) leftLabel.alpha = opacity;
        if (rightLabel != null) rightLabel.alpha = opacity;
    }

    private void DrawHeading(Node node)
    {
        heading = node;
        if (currentLabel != null) currentLabel.text = node.ToString().ToUpperInvariant();
        if (leftLabel != null) leftLabel.text = node == Node.Deployment ? "WEAPONS" : "MEDICINE";
        if (rightLabel != null) rightLabel.text = node == Node.Medicine ? "WEAPONS" : "DEPLOYMENT";
        if (leftButton != null) leftButton.gameObject.SetActive(node != Node.Medicine);
        if (rightButton != null) rightButton.gameObject.SetActive(node != Node.Deployment);
    }
}
