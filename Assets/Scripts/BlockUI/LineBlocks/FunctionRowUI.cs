using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Linha de nome dentro do bloco de declaração de função.
// Parâmetro: [Nome] [Tipo]                          [x]
// Variável:  [Nome] [Tipo] [Valor Default]          [x]
// Só nome + tipo são obrigatórios; os campos de default só aparecem quando hasDefault é true.
public class FunctionRowUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button removeButton;
    [SerializeField] private TMP_Dropdown typeDropdown;
    [SerializeField] private TMP_Dropdown defaultValueDropdown;
    [SerializeField] private TMP_InputField defaultIntInput;

    [SerializeField] private SectionDatabase sectionDatabase;
    [SerializeField] private string defaultParamName = "novo parâmetro";
    [SerializeField] private string defaultLocalName = "nova variável";

    private bool hasDefault;
    private ParameterType currentType = ParameterType.IntParameter;
    private string currentDefault;
    private System.Action<FunctionRowUI> onRemove;
    private System.Action onChanged;

    public string Name => nameInput != null ? nameInput.text : string.Empty;
    public ParameterType Type => currentType;
    // usado no save das variáveis locais: seção derivada do tipo + o valor default corrente
    public ParameterSections Section => ParameterSetup.SectionForType(currentType);
    public string DefaultValue => currentDefault;

    private void Awake() {
        if (removeButton != null) removeButton.onClick.AddListener(HandleRemove);
        if (nameInput != null) nameInput.onEndEdit.AddListener(HandleNameEndEdit);
        if (typeDropdown != null) typeDropdown.onValueChanged.AddListener(HandleTypeChanged);
        if (defaultValueDropdown != null) defaultValueDropdown.onValueChanged.AddListener(HandleDefaultChanged);
        if (defaultIntInput != null) defaultIntInput.onEndEdit.AddListener(HandleDefaultIntEdit);
    }

    // misc.setup do tipo do dropdown de tipo com os ParameterTypes disponíveis (uma vez, feito no prefab)
    public void SetupDefaultOptions() {
        if (typeDropdown == null) {
            Debug.LogWarning("[FunctionRowUI] typeDropdown não atribuído no prefab.");
            return;
        }
        typeDropdown.ClearOptions();
        var names = new List<string>();
        foreach (ParameterType type in System.Enum.GetValues(typeof(ParameterType))) {
            // EntityParameter é usado apenas na operação "contém" (parâmetro booleano)
            // — não deve aparecer como tipo de parâmetro/variável de função.
            if (type == ParameterType.EntityParameter) continue;
            names.Add(ParameterSetup.ParameterTypeLabel(type));
        }
        typeDropdown.AddOptions(names);
    }

    // Parâmetro: valor vem da chamada, então sem campo de default.
    public void SetupParameter(string paramName, ParameterType type, System.Action<FunctionRowUI> remove, System.Action changed) {
        hasDefault = false;
        onRemove = remove;
        onChanged = changed;
        SetupDefaultOptions();
        ApplyType(type, false);
        if (nameInput != null) nameInput.SetTextWithoutNotify(string.IsNullOrEmpty(paramName) ? defaultParamName : paramName);
        if (defaultValueDropdown != null) defaultValueDropdown.gameObject.SetActive(false);
        if (defaultIntInput != null) defaultIntInput.gameObject.SetActive(false);
        RebuildSelf();
    }

    // Variável local: tem valor default próprio (tipo define as opções, como no CreateVariableUI).
    public void SetupLocal(string localName, ParameterType type, string defaultValue, System.Action<FunctionRowUI> remove, System.Action changed) {
        hasDefault = true;
        onRemove = remove;
        onChanged = changed;
        SetupDefaultOptions();
        ApplyType(type, true);
        if (nameInput != null) nameInput.SetTextWithoutNotify(string.IsNullOrEmpty(localName) ? defaultLocalName : localName);
        ApplyDefault(defaultValue);
        RebuildSelf();
    }

    private void HandleNameEndEdit(string text) {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text)) {
            text = hasDefault ? defaultLocalName : defaultParamName;
            if (nameInput != null) nameInput.SetTextWithoutNotify(text);
        }
        onChanged?.Invoke();
    }

    private void HandleRemove() => onRemove?.Invoke(this);

    private void HandleTypeChanged(int index) {
        if (index < 0 || index >= typeDropdown.options.Count) return;
        if (index >= System.Enum.GetValues(typeof(ParameterType)).Length) return;
        var type = (ParameterType)index;
        ApplyType(type, true);
        onChanged?.Invoke();
    }

    private void ApplyType(ParameterType type, bool notifyDefault) {
        currentType = type;
        if (typeDropdown != null) {
            int index = (int)type;
            // EntityParameter não está no dropdown; cai no primeiro item.
            if (index >= typeDropdown.options.Count) index = 0;
            typeDropdown.SetValueWithoutNotify(index);
        }
        _ = notifyDefault;
        RebuildDefaultOptions();
    }

    private void RebuildDefaultOptions() {
        if (!hasDefault) return;

        var section = ParameterSetup.SectionForType(currentType);
        var values = sectionDatabase != null ? sectionDatabase.GetValuesForSection(section) : new List<string>();
        bool isInt = section == ParameterSections.IntValues;

        if (defaultValueDropdown != null) defaultValueDropdown.gameObject.SetActive(!isInt);
        if (defaultIntInput != null) defaultIntInput.gameObject.SetActive(isInt);

        if (isInt) {
            if (defaultIntInput != null) defaultIntInput.SetTextWithoutNotify(string.IsNullOrEmpty(currentDefault) ? "0" : currentDefault);
        } else if (defaultValueDropdown != null) {
            defaultValueDropdown.ClearOptions();
            defaultValueDropdown.AddOptions(values);
            currentDefault = values.Count > 0 ? values[0] : null;
            defaultValueDropdown.SetValueWithoutNotify(0);
        }

        RebuildSelf();
    }

    private void ApplyDefault(string defaultValue) {
        if (!hasDefault) return;

        currentDefault = defaultValue;
        var section = ParameterSetup.SectionForType(currentType);

        if (section == ParameterSections.IntValues) {
            if (defaultIntInput != null) defaultIntInput.SetTextWithoutNotify(string.IsNullOrEmpty(defaultValue) ? "0" : defaultValue);
        } else if (defaultValueDropdown != null) {
            var values = sectionDatabase != null ? sectionDatabase.GetValuesForSection(section) : new List<string>();
            int index = values.IndexOf(defaultValue);
            if (index >= 0) defaultValueDropdown.SetValueWithoutNotify(index);
        }
    }

    private void HandleDefaultChanged(int index) {
        if (!hasDefault || defaultValueDropdown == null) return;
        if (index < 0 || index >= defaultValueDropdown.options.Count) return;
        currentDefault = defaultValueDropdown.options[index].text;
        onChanged?.Invoke();
    }

    private void HandleDefaultIntEdit(string text) {
        if (!hasDefault) return;
        currentDefault = text;
        onChanged?.Invoke();
    }

    public void CleanUp() {
        if (nameInput != null) nameInput.onEndEdit.RemoveAllListeners();
        if (typeDropdown != null) typeDropdown.onValueChanged.RemoveAllListeners();
        if (defaultValueDropdown != null) defaultValueDropdown.onValueChanged.RemoveAllListeners();
        if (defaultIntInput != null) defaultIntInput.onEndEdit.RemoveAllListeners();
        if (removeButton != null) removeButton.onClick.RemoveAllListeners();

        if (typeDropdown != null) typeDropdown.ClearOptions();
        if (defaultValueDropdown != null) defaultValueDropdown.ClearOptions();

        currentDefault = null;
        hasDefault = false;
        onRemove = null;
        onChanged = null;
    }

    private void RebuildSelf() {
        var rect = GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }
}
