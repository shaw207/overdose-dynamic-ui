using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Object = UnityEngine.Object;

/// <summary>Searchable forge catalogue on a station computer with a sliding inspection pane.</summary>
public sealed class AndyStationComputer : MonoBehaviour
{
    [SerializeField] private RectTransform listPane, detailPane, resultsRoot;
    [SerializeField] private CanvasGroup details;
    [SerializeField] private TMP_InputField search;
    [SerializeField] private WeaponEntryUI resultTemplate;
    [SerializeField] private GameObject emptyResults;
    [SerializeField] private TMP_Text detailName, detailDescription, detailStats;
    [SerializeField] private Button closeDetail, categoryButton;
    [SerializeField] private TMP_Text categoryLabel;
    [SerializeField] private RectTransform categoryMenu;
    [SerializeField] private Button categoryTemplate;

    private sealed class Entry
    {
        public Object item;
        public string id, title, category;
        public WeaponEntryUI row;
        public UnityAction click;
    }

    private readonly List<Entry> entries = new();
    private readonly List<Button> options = new();
    private Object selected;
    private string category = string.Empty;
    private float progress;
    private bool entering = true;

    public event Action<Object> ItemPressed;
    public event Action Dismissed;
    public bool IsSearchFocused => isActiveAndEnabled && search != null && search.isFocused;

    private void OnEnable()
    {
        entering = true;
        search.onValueChanged.AddListener(ChangeQuery);
        closeDetail.onClick.AddListener(Dismiss);
        if (categoryButton != null) categoryButton.onClick.AddListener(ToggleCategories);
    }

    private void OnDisable()
    {
        search.onValueChanged.RemoveListener(ChangeQuery);
        closeDetail.onClick.RemoveListener(Dismiss);
        if (categoryButton != null) categoryButton.onClick.RemoveListener(ToggleCategories);
        if (search.isFocused) search.DeactivateInputField();
        ResetFilters();
    }

    private void Dismiss() => Dismissed?.Invoke();

    public void ResetFilters()
    {
        search.SetTextWithoutNotify(string.Empty);
        SetCategory(string.Empty);
    }

    public void ClearEntries()
    {
        foreach (var entry in entries)
        {
            if (entry.row == null) continue;
            entry.row.Button.onClick.RemoveListener(entry.click);
            entry.row.gameObject.SetActive(false);
            Destroy(entry.row.gameObject);
        }
        entries.Clear();
        foreach (var button in options)
            if (button != null) { button.gameObject.SetActive(false); Destroy(button.gameObject); }
        options.Clear();
    }

    public bool HasCatalogue<T>(IReadOnlyList<T> catalogue) where T : Object
    {
        int index = 0;
        foreach (var item in catalogue)
        {
            if (item == null) continue;
            if (index >= entries.Count || entries[index].item != item) return false;
            index++;
        }
        return index == entries.Count;
    }

    public void Add(Object item, string id, string title, Sprite icon, string type = "")
    {
        var row = Instantiate(resultTemplate,resultsRoot);
        row.name = title;
        row.Bind(new WeaponViewData { id=id, name=title, icon=icon });
        row.GetComponentInChildren<TMP_Text>(true).text = title;
        UnityAction click = () => ItemPressed?.Invoke(item);
        row.Button.onClick.AddListener(click);
        entries.Add(new Entry { item=item, id=id, title=title, category=type, row=row, click=click });
    }

    public void CompleteCatalogue()
    {
        if (categoryButton != null)
        {
            AddCategory(string.Empty);
            var unique = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
                if (!string.IsNullOrEmpty(entry.category)) unique.Add(entry.category);
            foreach (string type in unique) AddCategory(type);
        }
        SetCategory(string.Empty);
    }

    private void AddCategory(string value)
    {
        var button = Instantiate(categoryTemplate, categoryMenu);
        button.name = string.IsNullOrEmpty(value) ? "All" : value;
        button.GetComponentInChildren<TMP_Text>(true).text = button.name;
        button.onClick.AddListener(() => SetCategory(value));
        options.Add(button);
        button.gameObject.SetActive(true);
        for (int i=0; i<options.Count; i++)
        {
            var rect = (RectTransform)options[i].transform;
            rect.anchorMin = new Vector2(0,1f-(i+1f)/options.Count);
            rect.anchorMax = new Vector2(1,1f-i/(float)options.Count);
            rect.offsetMin = Vector2.one;
            rect.offsetMax = -Vector2.one;
        }
    }

    private void ToggleCategories() => categoryMenu.gameObject.SetActive(!categoryMenu.gameObject.activeSelf);

    private void SetCategory(string value)
    {
        category = value;
        if (categoryLabel != null) categoryLabel.text = string.IsNullOrEmpty(value) ? "All" : value;
        if (categoryMenu != null) categoryMenu.gameObject.SetActive(false);
        ChangeQuery(search.text);
    }

    private void ChangeQuery(string value)
    {
        string query = (value ?? string.Empty).Trim();
        int shown = 0;
        foreach (var entry in entries)
        {
            bool matches = (category.Length==0 || category==entry.category) &&
                (entry.title.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 ||
                 entry.id.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
            entry.row.gameObject.SetActive(matches);
            if (matches) shown++;
        }
        emptyResults.SetActive(shown==0);
        const int visibleRows=6;
        int rows=Mathf.Max(visibleRows,shown);
        resultsRoot.anchorMin=new Vector2(0,1f-rows/(float)visibleRows);
        resultsRoot.anchorMax=Vector2.one;
        resultsRoot.anchoredPosition=Vector2.zero;
        int index=0;
        foreach(var entry in entries)
        {
            if(!entry.row.gameObject.activeSelf) continue;
            var rect=(RectTransform)entry.row.transform;
            rect.anchorMin=new Vector2(0,1f-(index+1f)/rows);
            rect.anchorMax=new Vector2(1,1f-index/(float)rows);
            rect.offsetMin=new Vector2(0,2);
            rect.offsetMax=new Vector2(0,-2);
            index++;
        }
    }

    public void ShowDetails(Object item,string name,string description,string stats)
    {
        selected=item;
        if(item!=null)
        {
            detailName.text=name;
            detailDescription.text=description;
            detailStats.text=stats;
        }
        foreach(var entry in entries) entry.row.SetSelected(entry.item==item);
        bool showing=item!=null;
        if(showing) detailPane.gameObject.SetActive(true);
        details.interactable=showing;
        details.blocksRaycasts=showing;
        if(entering)
        {
            progress=showing ? 1f : 0f;
            Layout();
            entering=false;
        }
    }

    private void Update()
    {
        float target=selected!=null ? 1f : 0f;
        if(progress==target) return;
        progress=Mathf.MoveTowards(progress,target,Time.unscaledDeltaTime/.22f);
        Layout();
    }

    private void Layout()
    {
        float t=Mathf.SmoothStep(0,1,progress);
        float split=Mathf.Lerp(1f,.42f,t);
        listPane.anchorMax=new Vector2(split,1f);
        detailPane.anchorMin=new Vector2(split,0f);
        details.alpha=t;
        details.blocksRaycasts=details.interactable=selected!=null && t>.80f;
        if(progress==0f) detailPane.gameObject.SetActive(false);
    }
}
