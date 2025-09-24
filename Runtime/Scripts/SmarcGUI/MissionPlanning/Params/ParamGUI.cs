using System.Collections;
using System.Collections.Generic;
using GeoRef;
using SmarcGUI.MissionPlanning.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmarcGUI.MissionPlanning.Params
{
    public class ParamGUI :
    MonoBehaviour,
    IListItem,
    IHeightUpdatable,
    IPointerClickHandler,
    IPointerExitHandler,
    IPointerEnterHandler
    {
        [Header("ParamGUI")]
        public TMP_Text Label;
        public RectTransform HighlightRT;

        protected IDictionary paramsDict;
        public string ParamKey { get; protected set; }
        protected TaskGUI taskgui;

        protected IList paramsList;
        public int ParamIndex { get; protected set; }
        protected ListParamGUI listParamGUI;

        protected MissionPlanStore missionPlanStore;
        protected GUIState guiState;

        protected RectTransform rt;

        protected List<RectTransform> fields = new();

        GlobalReferencePoint globalReferencePoint;

        public object paramValue
        {
            get => paramsDict != null ? paramsDict[ParamKey] : paramsList[ParamIndex];
            protected set
            {
                if (paramsDict != null)
                    paramsDict[ParamKey] = value;
                else
                    paramsList[ParamIndex] = value;
            }
        }

        void Awake()
        {
            missionPlanStore = FindFirstObjectByType<MissionPlanStore>();
            guiState = FindFirstObjectByType<GUIState>();
            globalReferencePoint = FindFirstObjectByType<GlobalReferencePoint>();

            rt = GetComponent<RectTransform>();
            if (HighlightRT == null)
            {
                HighlightRT = transform.Find("Highlight").GetComponent<RectTransform>();
                HighlightRT.gameObject.SetActive(false);
            }
            if (HighlightRT == null)
            {
                Debug.LogError("HighlightRT is null in ParamGUI and could not be found");
            }
        }

        // We wrap the global reference point methods to allow
        // sub-classes to use them without needing to know about the tstgui stuff.
        protected (double, double) GetLatLonFromUnityXZ(float x, float z)
        {
            if (globalReferencePoint == null)
            {
                globalReferencePoint = FindFirstObjectByType<GlobalReferencePoint>();
                if (globalReferencePoint == null)
                {
                    Debug.LogError("GlobalReferencePoint is null in ParamGUI");
                    return (0, 0);
                }
            }
            return globalReferencePoint.GetLatLonFromUnityXZ(x, z);
        }

        protected (float x, float z) GetUnityXZFromLatLon(double lat, double lon)
        {
            if (globalReferencePoint == null)
            {
                globalReferencePoint = FindFirstObjectByType<GlobalReferencePoint>();
                if (globalReferencePoint == null)
                {
                    Debug.LogError("GlobalReferencePoint is null in ParamGUI");
                    return (0, 0);
                }
            }
            return globalReferencePoint.GetUnityXZFromLatLon(lat, lon);
        }

        public void SetParam(IDictionary paramsDict, string paramKey, TaskGUI taskgui)
        {
            this.paramsDict = paramsDict;
            this.ParamKey = paramKey;
            this.taskgui = taskgui;
            UpdateLabel();
            SetupFields();
        }
        public void SetParam(IList paramsList, int paramIndex, ListParamGUI listParamGUI)
        {
            this.paramsList = paramsList;
            this.ParamIndex = paramIndex;
            this.listParamGUI = listParamGUI;
            this.taskgui = listParamGUI.taskgui; // the taskgui is the one that contains this list param gui
            UpdateLabel();
            SetupFields();
            ReArrangeForList();
        }

        void UpdateLabel()
        {
            if (Label == null) return;
            Label.text = ParamKey ?? ParamIndex.ToString();
        }

        public void UpdateIndex(int newIndex)
        {
            ParamIndex = newIndex;
            UpdateLabel();
        }

        protected virtual void SetupFields()
        {
            throw new System.NotImplementedException();
        }

        public List<RectTransform> GetFields()
        {
            return fields;
        }

        public virtual List<string> GetFieldLabels()
        {
            throw new System.NotImplementedException();
        }

        protected void NotifyPathChange()
        {
            if (taskgui != null) taskgui.OnParamChanged();
            if (listParamGUI != null) listParamGUI.OnParamChanged();
        }

        protected virtual void OnSelectedChange()
        {
            return;
        }

        public void OnListItemUp()
        {
            listParamGUI.MoveParamUp(this);
            NotifyPathChange();
        }

        public void OnListItemDown()
        {
            listParamGUI.MoveParamDown(this);
            NotifyPathChange();
        }

        public void OnListItemDelete()
        {
            listParamGUI.DeleteParam(this);
            NotifyPathChange();
        }

        public void UpdateHeight()
        {
            float selfHeight = 5;
            foreach (Transform child in transform)
            {
                if (child.gameObject.activeSelf)
                    selfHeight += child.GetComponent<RectTransform>().sizeDelta.y;
            }

            rt.sizeDelta = new Vector2(rt.sizeDelta.x, selfHeight);
        }

        public void ReArrangeForList()
        {
            if (listParamGUI == null) return;
            // we want to re-arrange all the fields in of this param
            // into one line, without any labels, without changing their sizes.
            // this is because the list param gui will take care of the labels for _all of the items_.
            // so, first move all the fields to one common parent
            GameObject fieldsParent = new("Fields");
            fieldsParent.transform.SetParent(transform);
            var fieldsRT = fieldsParent.AddComponent<RectTransform>();
            var fieldsLayout = fieldsParent.AddComponent<HorizontalLayoutGroup>();
            fieldsLayout.childControlHeight = false;
            fieldsLayout.childControlWidth = false;
            fieldsLayout.childForceExpandHeight = false;
            fieldsLayout.childForceExpandWidth = false;
            fieldsLayout.childScaleHeight = false;
            fieldsLayout.childScaleWidth = true;
            fieldsLayout.childAlignment = TextAnchor.MiddleLeft;
            fieldsLayout.spacing = 0;
            fieldsLayout.padding = new RectOffset(0, 0, 0, 0);

            // then, move all the fields to this new parent
            float totalChildrenWidth = 0;
            float maxChildHeight = 0;
            foreach (var field in fields)
            {
                field.transform.SetParent(fieldsParent.transform);
                totalChildrenWidth += field.sizeDelta.x;
                if (field.sizeDelta.y > maxChildHeight)
                    maxChildHeight = field.sizeDelta.y;
            }

            // also add a new label to the left-hand side of the fields
            var label = new GameObject("Label");
            label.transform.SetParent(fieldsParent.transform);
            label.transform.SetAsFirstSibling();
            label.transform.localScale = fields[0].transform.localScale; // match the scale of the first field
            var labelRT = label.AddComponent<RectTransform>();
            var labelWidth = 10;
            labelRT.sizeDelta = new Vector2(labelWidth, maxChildHeight);
            label.AddComponent<TextMeshProUGUI>();
            var labelText = label.GetComponent<TMP_Text>();
            labelText.text = ParamIndex.ToString();
            labelText.enableAutoSizing = true;
            Label = labelText; // change the label of the param to this one

            if (rt == null) rt = GetComponent<RectTransform>();
            var widthRemainingAfterLabel = rt.sizeDelta.x - labelWidth;

            rt.sizeDelta = new Vector2(rt.sizeDelta.x, maxChildHeight);
            // if the total width of the children is greater than the width of the parent, we need to resize them
            // so that they fit in the parent
            if (totalChildrenWidth > widthRemainingAfterLabel)
            {
                var diff = totalChildrenWidth - widthRemainingAfterLabel;
                var per = diff / fields.Count;
                foreach (var field in fields)
                {
                    field.sizeDelta = new Vector2(field.sizeDelta.x - per, field.sizeDelta.y);
                }
                totalChildrenWidth -= diff;
            }
            // set the size of the new parent to be the sum of all the children widths, and the max height of the children
            fieldsRT.sizeDelta = new Vector2(totalChildrenWidth + labelWidth, maxChildHeight);
            fieldsRT.anchoredPosition = Vector2.zero;
            fieldsRT.pivot = new Vector2(0, 0.5f);
            fieldsRT.anchorMin = new Vector2(0, 0.5f);
            fieldsRT.anchorMax = new Vector2(0, 0.5f);

            // disable all other children of this object
            foreach (Transform child in transform)
            {
                if (child != fieldsParent.transform)
                    child.gameObject.SetActive(false);
            }

        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                var contextMenu = guiState.CreateContextMenu();
                contextMenu.SetItem(eventData.position, (IListItem)this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (HighlightRT != null) HighlightRT.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (HighlightRT != null) HighlightRT.gameObject.SetActive(false);
        }
        

    }
}