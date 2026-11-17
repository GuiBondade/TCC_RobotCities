using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RobotInterface : MonoBehaviour
{
    [SerializeField] private ScriptManager scriptManager;
    [SerializeField] private RepositionInterface repositionInterface;
    [SerializeField] private Transform layoutOptionContent;
    [SerializeField] private LayoutOption layoutOptionPrefab;
    [SerializeField] private Transform slotInterfaceContent;
    [SerializeField] private SlotInterface slotInterfacePrefab;
    [SerializeField] private MaterialDatabase materialDatabase;
    [SerializeField] private ToolDatabase toolDatabase;
    [SerializeField] private SlotInterface toolSlotInterface;
    private Transform toolSlotContent;

    [SerializeField] private ScriptData scriptChosen; 
    [SerializeField] private TMP_InputField nameInput;
    //ferramenta
    //inventario
    [SerializeField] private ScriptDropdown scriptDropdown;
    [SerializeField] private CustomTextToggle executionToggle;
    [SerializeField] private CustomTextToggle debugToggle;

    private RobotScript robotScript;
    private RobotData data;
    private bool populating;

    public void SetSignal() {
        RobotSignals.OnInterfacePopulation -= SetupData;
        if (repositionInterface != null) RobotSignals.OnInterfaceReposition -= repositionInterface.SetupPosition;
        RobotSignals.OnInterfacePopulation += SetupData; 
        if (repositionInterface != null) RobotSignals.OnInterfaceReposition += repositionInterface.SetupPosition;
        if (executionToggle != null) {
            executionToggle.OnToggleStateChanged -= ExecutionToggled;
            executionToggle.OnToggleStateChanged += ExecutionToggled;
        }
        if (debugToggle != null) {
            debugToggle.OnToggleStateChanged -= DebugToggled;
            debugToggle.OnToggleStateChanged += DebugToggled;
        }
        if (nameInput != null) {
            nameInput.onEndEdit.RemoveListener(OnNameEdited);
            nameInput.onEndEdit.AddListener(OnNameEdited);
        }
    }

    private void OnDestroy() {
        RobotSignals.OnInterfacePopulation -= SetupData;
        if (repositionInterface != null) RobotSignals.OnInterfaceReposition -= repositionInterface.SetupPosition;
        if (executionToggle != null) executionToggle.OnToggleStateChanged -= ExecutionToggled;
        if (debugToggle != null) debugToggle.OnToggleStateChanged -= DebugToggled;
        if (nameInput != null) nameInput.onEndEdit.RemoveListener(OnNameEdited);
        UnsubscribeFromInventory();
    }

    private void SetupData(RobotScript script) {
        if (this == null || script == null) return;

        populating = true;
        try {
            /* a partir do RobotData setar:
            nome do robo(inputText)
            ferramenta(não sei como, carrossel? dropdown de imagem?)
            inventario(tem que fazer tudo ainda)
            index do script escolhido(value do dropdown)
            bool de em execução(colorir background do botao pra verde ou escuro)
            bool de debug ativado(colorir background do botao pra verde ou escuro)
             */ // a população da interface não dispara os eventos dos componentes
            robotScript = script;
            data = robotScript.robotData;
            if (data == null) {
                Debug.LogError("[RobotInterface] Robô sem RobotData.", robotScript);
                return;
            }

            SubscribeToInventory();
            scriptChosen = robotScript.scriptData;

            PopulateLayouts();
            PopulateSlots();
            PopulateToolSlot();

            if (nameInput != null) nameInput.SetTextWithoutNotify(data.robotName ?? string.Empty);

            // A UI deve refletir o script realmente anexado, não um índice salvo desatualizado.
            if (scriptChosen != null && scriptManager != null && scriptManager.scripts != null) {
                int actualIndex = scriptManager.scripts.IndexOf(scriptChosen);
                if (actualIndex >= 0) data.scriptIndex = actualIndex + 1;
            }

            scriptDropdown.UpdateDropdown(scriptManager.scripts, data.scriptIndex);

            bool isExecuting = robotScript.executionContext != null;
            data.isExecuting = isExecuting;
            executionToggle.SetStateWithoutNotify(isExecuting);
            debugToggle.SetStateWithoutNotify(data.isDebugging);

            gameObject.SetActive(true);
        } finally {
            populating = false;
        }
    }

    private void SubscribeToInventory() {
        UnsubscribeFromInventory();
        if (robotScript != null && robotScript.robotInventory != null
            && robotScript.robotInventory.inventoryManager != null) {
            robotScript.robotInventory.inventoryManager.OnInventoryChanged += OnInventoryChanged;
        }
    }

    private void UnsubscribeFromInventory() {
        if (robotScript != null && robotScript.robotInventory != null
            && robotScript.robotInventory.inventoryManager != null) {
            robotScript.robotInventory.inventoryManager.OnInventoryChanged -= OnInventoryChanged;
        }
    }

    private void OnInventoryChanged(InventoryManager inventory) {
        if (!gameObject.activeInHierarchy || robotScript == null
            || robotScript.robotInventory == null
            || inventory != robotScript.robotInventory.inventoryManager) return;
        PopulateSlots();
        PopulateLayouts();
    }

    private void PopulateLayouts() {
        if (robotScript == null || robotScript.robotInventory == null) return;
        if (layoutOptionContent == null || layoutOptionPrefab == null || materialDatabase == null) {
            Debug.LogWarning("[RobotInterface] Sistema de layouts não configurado completamente.", this);
            return;
        }

        var layouts = robotScript.robotInventory.GetAvailableLayouts();
        while (layoutOptionContent.childCount < layouts.Count) {
            Instantiate(layoutOptionPrefab, layoutOptionContent);
        }

        for (int i = 0; i < layoutOptionContent.childCount; i++) {
            var layoutInstance = layoutOptionContent.GetChild(i);
            if (i >= layouts.Count) {
                layoutInstance.gameObject.SetActive(false);
                continue;
            }

            layoutInstance.gameObject.SetActive(true);
            var layoutOption = layoutInstance.GetComponent<LayoutOption>();
            var layout = layouts[i];
            if (layoutOption == null || layoutOption.icone == null || layoutOption.button == null) {
                Debug.LogError("[RobotInterface] LayoutOption incompleto no prefab.", layoutInstance);
                continue;
            }

            if (layoutOption.nome != null) layoutOption.nome.text = layout.recipeConfigName;
            layoutOption.icone.sprite = GetLayoutIcon(layout);
            layoutOption.button.onClick.RemoveAllListeners();
            layoutOption.button.onClick.AddListener(() => {
                robotScript.robotInventory.SelectLayout(layout);
                PopulateLayouts();
                PopulateSlots();
                PopulateToolSlot();
            });

            if (robotScript.robotInventory.currentLayout == layout) {
                layoutOption.ActivateLayout();
            } else if (robotScript.robotInventory.inventoryManager != null
                && robotScript.robotInventory.inventoryManager.IsEmpty()) {
                layoutOption.ActivateLayout();
            } else {
                layoutOption.DeactivateLayout();
            }
        }
    }

    private Sprite GetLayoutIcon(SlotLayoutConfig layout) {
        if (layout == null) return null;
        if (layout.icon != null) return layout.icon;
        if (layout.slotConfigList == null) return null;

        foreach (var slot in layout.slotConfigList) {
            if (!slot.hasTypeLocked) continue;
            if (slot.role == SlotRole.Tool) {
                ToolData tool = toolDatabase != null ? toolDatabase.GetById(slot.typeId) : null;
                if (tool != null && tool.icon != null) return tool.icon;
                continue;
            }
            var material = materialDatabase.ObterPorId(slot.typeId);
            if (material.icon != null) return material.icon;
        }
        return null;
    }

    private void PopulateToolSlot() {
        if (toolSlotInterface == null) {
            EnsureToolSlotContent();
            if (toolSlotContent != null && slotInterfacePrefab != null) {
                toolSlotInterface = Instantiate(slotInterfacePrefab, toolSlotContent);
            }
        }
        if (toolSlotInterface == null) return;
        if (robotScript == null || robotScript.robotInventory == null
            || robotScript.robotInventory.inventoryManager == null) {
            toolSlotInterface.ShowEmpty();
            return;
        }
        var toolSlot = robotScript.robotInventory.GetToolSlot();
        if (toolSlot == null || toolSlot.currentTypeId < 0 || toolSlot.currentAmount <= 0) {
            toolSlotInterface.ShowEmpty();
            return;
        }
        ToolData tool = toolDatabase != null ? toolDatabase.GetById(toolSlot.currentTypeId) : null;
        if (toolDatabase == null && ExecutionManager.instancia != null) {
            tool = ExecutionManager.instancia.GetTool(toolSlot.currentTypeId);
        }
        toolSlotInterface.UpdateToolSlot(tool, toolSlot.currentAmount, toolSlot.maxAmount);
    }

    private void PopulateSlots() {
        if (robotScript == null || robotScript.robotInventory == null || robotScript.robotInventory.inventoryManager == null) {
            return;
        }
        if (slotInterfacePrefab == null || materialDatabase == null) {
            Debug.LogWarning("[RobotInterface] Slot inventory não configurado completamente.", this);
            return;
        }
        EnsureSlotContent();

        var slotList = robotScript.robotInventory.inventoryManager.slotList;

        // Desativa tudo antes de re-renderizar (evita slots stale de layouts anteriores).
        for (var d = 0; d < slotInterfaceContent.childCount; d++) {
            slotInterfaceContent.GetChild(d).gameObject.SetActive(false);
        }

        int uiIndex = 0;
        for (var i = 0; i < slotList.Count; i++) {
            var slotData = slotList[i];
            if (slotData.role == SlotRole.Tool) continue; // ferramenta tem área própria

            MaterialData material = slotData.currentTypeId >= 0
                ? materialDatabase.ObterPorId(slotData.currentTypeId)
                : default;
            if (slotData.currentTypeId < 0 || !slotData.hasTypeLocked) {
                material = default;
            }

            SlotInterface slotInterface;
            if (uiIndex >= slotInterfaceContent.childCount) {
                slotInterface = Instantiate(slotInterfacePrefab, slotInterfaceContent);
            } else {
                slotInterface = slotInterfaceContent.GetChild(uiIndex).GetComponent<SlotInterface>();
            }

            if (slotInterface == null) {
                Debug.LogError("[RobotInterface] SlotInterface instanciado sem o componente correto.", this);
                uiIndex++;
                continue;
            }

            slotInterface.gameObject.SetActive(true);
            if (slotData.currentTypeId < 0 && !slotData.hasTypeLocked) {
                slotInterface.ShowEmpty();
            } else {
                slotInterface.UpdateSlot(slotData.currentAmount, material);
            }
            uiIndex++;
        }
    }

    private void EnsureSlotContent() {
        if (slotInterfaceContent != null && slotInterfaceContent != layoutOptionContent) return;

        var parent = layoutOptionContent != null ? layoutOptionContent.parent : transform;
        var slotContentObject = new GameObject("RobotSlotContent", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        slotContentObject.transform.SetParent(parent, false);
        var layout = slotContentObject.GetComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 8f;
        slotInterfaceContent = slotContentObject.transform;
    }

    private void EnsureToolSlotContent() {
        if (toolSlotContent != null) return;
        var parent = slotInterfaceContent != null ? slotInterfaceContent.parent : transform;
        var toolContentObject = new GameObject("RobotToolSlotContent", typeof(RectTransform));
        toolContentObject.transform.SetParent(parent, false);
        var rt = toolContentObject.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0f, -130f);
        rt.sizeDelta = new Vector2(110f, 110f);
        toolSlotContent = toolContentObject.transform;
    }

    private void DropdownOptionChosen(int index){
        if (populating || data == null || robotScript == null) return;

        // Trocar o script (ou limpar a seleção) interrompe a execução atual.
        bool wasExecuting = robotScript.executionContext != null;
        data.isExecuting = false;
        executionToggle.SetStateWithoutNotify(false);
        if (wasExecuting) RobotSignals.TriggerExecutionDeactivation(robotScript);

        data.scriptIndex = index;
        if (index == 0) {
            scriptChosen = null;
            robotScript.scriptData = null;
            return;
        }

        int scriptListIndex = index - 1;
        if (scriptManager == null || scriptManager.scripts == null
            || scriptListIndex < 0 || scriptListIndex >= scriptManager.scripts.Count) {
            return;
        }

        scriptChosen = scriptManager.scripts[scriptListIndex];
        robotScript.scriptData = scriptChosen;
    }

    private void ExecutionToggled(bool isOn) {
        if (populating || data == null || robotScript == null) return;

        if (isOn) {
            if (robotScript.scriptData == null) {
                Debug.LogWarning("[RobotInterface] Selecione um script antes de executar.", this);
                data.isExecuting = false;
                executionToggle.SetStateWithoutNotify(false);
                return;
            }

            data.isExecuting = true;
            RobotSignals.TriggerExecutionActivation(robotScript);
        } else {
            data.isExecuting = false;
            RobotSignals.TriggerExecutionDeactivation(robotScript);
        }
    }

    private void DebugToggled(bool isOn) {
        if (populating || data == null) return;
        data.isDebugging = isOn;
    }

    private void OnNameEdited(string newName) {
        if (populating || data == null || robotScript == null) return;

        newName = newName != null ? newName.Trim() : string.Empty;
        if (string.IsNullOrEmpty(newName)) {
            if (nameInput != null) nameInput.SetTextWithoutNotify(data.robotName ?? string.Empty);
            return;
        }

        data.robotName = newName;
        robotScript.SetDisplayName(newName);
    }
}
