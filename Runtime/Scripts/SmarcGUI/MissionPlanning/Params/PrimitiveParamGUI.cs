using TMPro;
using System.Collections.Generic;
using UnityEngine;



namespace SmarcGUI.MissionPlanning.Params
{

    class PrimitiveParamGUI : ParamGUI
    {
        [Header("PrimitiveParamGUI")]
        public TMP_InputField InputField;
        public TMP_Dropdown ChoiceDropdown;
        

        protected override void SetupFields()
        {
            switch (paramValue)
            {
                case string s:
                    InputField.gameObject.SetActive(true);
                    InputField.text = s;
                    InputField.contentType = TMP_InputField.ContentType.Standard;
                    InputField.onEndEdit.AddListener(OnInputFieldChanged);
                    break;
                case int i:
                    InputField.gameObject.SetActive(true);
                    InputField.text = i.ToString();
                    InputField.contentType = TMP_InputField.ContentType.IntegerNumber;
                    InputField.onEndEdit.AddListener(OnInputFieldChanged);
                    break;
                case float f:
                    InputField.gameObject.SetActive(true);
                    InputField.text = f.ToString();
                    InputField.contentType = TMP_InputField.ContentType.DecimalNumber;
                    InputField.onEndEdit.AddListener(OnInputFieldChanged);
                    break;
                case bool b:
                    ChoiceDropdown.gameObject.SetActive(true);
                    ChoiceDropdown.ClearOptions();
                    ChoiceDropdown.AddOptions(new List<string>{"True", "False"});
                    ChoiceDropdown.onValueChanged.AddListener(OnChoiceChanged);
                    break;
                case double d:
                    InputField.gameObject.SetActive(true);
                    InputField.text = d.ToString();
                    InputField.contentType = TMP_InputField.ContentType.DecimalNumber;
                    InputField.onEndEdit.AddListener(OnInputFieldChanged);
                    break;
                case long l:
                    InputField.gameObject.SetActive(true);
                    InputField.text = l.ToString();
                    InputField.contentType = TMP_InputField.ContentType.IntegerNumber;
                    InputField.onEndEdit.AddListener(OnInputFieldChanged);
                    break;
                default:
                    InputField.gameObject.SetActive(true);
                    InputField.text = $"Non-primitive type: {paramValue.GetType()}";
                    InputField.contentType = TMP_InputField.ContentType.Standard;
                    InputField.interactable = false;
                    break;
            }
            if(paramValue is bool)
                fields.Add(ChoiceDropdown.GetComponent<RectTransform>());
            else
                fields.Add(InputField.GetComponent<RectTransform>());
        }

        public override List<string> GetFieldLabels()
        {
            return new List<string> { "Primitive" };
        }

        void OnInputFieldChanged(string value)
        {
            switch(InputField.contentType)
            {
                case TMP_InputField.ContentType.Standard:
                    paramValue = value;
                    break;
                case TMP_InputField.ContentType.IntegerNumber:
                    if(int.TryParse(value, out int i))
                        paramValue = i;
                    break;
                case TMP_InputField.ContentType.DecimalNumber:
                    if(float.TryParse(value, out float f))
                        paramValue = f;
                    break;
            }
        }

        void OnChoiceChanged(int index)
        {
            paramValue = bool.Parse(ChoiceDropdown.options[index].text);
        }

    }
}
