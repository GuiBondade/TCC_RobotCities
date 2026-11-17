using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Script do GameObject "BuildingInputManager" (dentro de "BuildingLogic") na UIScene.
/// Fica responsável pelos botões de Construir/Destruir e por montar a paleta de construções.
/// Como o PlacementManager está no MapScene, toda a comunicação é feita por ConstructionSignals.
/// </summary>
public class BuildingInputManager : MonoBehaviour
{
    [Header("Paleta de construções")]
    [SerializeField] private List<ConstructionData> availableConstructions = new List<ConstructionData>();
    [SerializeField] private ConstructionPaletteItem paletteItemPrefab; // prefab do item da paleta
    [SerializeField] private Transform paletteContent; // pai onde os itens são instanciados
    /* [SerializeField] private GameObject palettePanel; // painel que mostra/esconde a paleta */

    [Header("Botões")]
    /* [SerializeField] private Button buildButton; // ButtonBuild */
    [SerializeField] private Button destroyButton; // ButtonDestroy

    // Reaproveitamento dos filhos da paleta (evita Destroy+Instantiate ao repopular em runtime).
    private readonly List<ConstructionPaletteItem> paletteItems = new List<ConstructionPaletteItem>();
    private readonly HashSet<string> loggedLocked = new HashSet<string>(StringComparer.Ordinal);
    private bool paletteInitialized;

    private void Start() {
        /* if (buildButton != null) buildButton.onClick.AddListener(ToggleBuildPalette); */
        if (destroyButton != null) destroyButton.onClick.AddListener(RequestDestroyMode);

        ConstructionSignals.OnConstructionSelected += OnConstructionSelected;

        PopulatePalette();

        ObjectiveSignals.OnUnlockGranted += HandleUnlockGranted;
        ObjectiveSignals.OnObjectivesReloaded += HandleObjectivesReloaded;

        // Se o ObjectiveManager já inicializou antes deste Start, sincroniza agora
        // (o evento OnObjectivesReloaded pode ter disparado antes da assinatura).
        if (ObjectiveManager.instancia != null && ObjectiveManager.instancia.IsInitialized) {
            HandleObjectivesReloaded();
        }

        /* if (palettePanel != null) palettePanel.SetActive(false); */
    }

    private void OnDestroy() {
        ConstructionSignals.OnConstructionSelected -= OnConstructionSelected;
        ObjectiveSignals.OnUnlockGranted -= HandleUnlockGranted;
        ObjectiveSignals.OnObjectivesReloaded -= HandleObjectivesReloaded;
    }

    /* /// <summary>Ligar no onClick do ButtonBuild.</summary>
    public void ToggleBuildPalette() {
        if (palettePanel == null) return;
        palettePanel.SetActive(!palettePanel.activeSelf);
    } */

    /// <summary>Ligar no onClick do ButtonDestroy.</summary>
    public void RequestDestroyMode() {
        /* ClosePalette(); */
        ConstructionSignals.TriggerDestroyModeRequested();
    }

    /// <summary>Fecha a paleta e cancela o modo de posicionamento/destruição ativo.</summary>
    public void CancelBuildMode() {
        /* ClosePalette(); */
        ConstructionSignals.TriggerPlacementCancelled();
    }

    private void OnConstructionSelected(ConstructionData data) {
        /* ClosePalette(); */
        return;
    }

    /* private void ClosePalette() {
        if (palettePanel != null) palettePanel.SetActive(false);
    } */

    private void HandleUnlockGranted(ObjectiveUnlockType type, string key, ObjectiveData source) {
        if (type != ObjectiveUnlockType.Construction) return;
        PopulatePalette();
    }

    private void HandleObjectivesReloaded() {
        PopulatePalette();
    }

    private void PopulatePalette() {
        if (paletteContent == null) return;

        // Coleta os itens já autorados na hierarquia (sem Destroy/Instantiate).
        // A ordem dos filhos deve corresponder à ordem de availableConstructions.
        if (!paletteInitialized) {
            paletteItems.Clear();
            for (int i = 0; i < paletteContent.childCount; i++) {
                var child = paletteContent.GetChild(i);
                var item = child.GetComponent<ConstructionPaletteItem>();
                if (item != null) {
                    paletteItems.Add(item);
                } else {
                    child.gameObject.SetActive(false);
                }
            }
            paletteInitialized = true;
        }

        // Mapeamento por índice: cada GO autorado corresponde a availableConstructions[i].
        for (int i = 0; i < availableConstructions.Count; i++) {
            var data = availableConstructions[i];

            if (i >= paletteItems.Count) {
                if (data != null) {
                    Debug.LogWarning($"[Paleta] Sem item autorado para '{GetKey(data)}' (índice {i}).", this);
                }
                continue;
            }

            var item = paletteItems[i];
            if (item == null) continue;

            if (data == null) {
                item.gameObject.SetActive(false);
                continue;
            }

            item.SetConstruction(data);

            bool available = IsConstructionAvailable(data);
            if (!available) {
                string key = GetKey(data);
                if (loggedLocked.Add(key)) {
                    Debug.Log($"[Paleta] '{key}' oculta: gerenciada por objetivo e ainda não desbloqueada.", this);
                }
            }
            item.gameObject.SetActive(available);
        }

        // Desativa itens excedentes (mais GOs autorados que construções na lista).
        for (int i = availableConstructions.Count; i < paletteItems.Count; i++) {
            if (paletteItems[i] != null) paletteItems[i].gameObject.SetActive(false);
        }
    }

    private static bool IsConstructionAvailable(ConstructionData data) {
        ObjectiveManager manager = ObjectiveManager.instancia;
        if (data == null || manager == null) return true;        // sem manager: mostra tudo
        if (!manager.IsConstructionManaged(data)) return true;   // não citada em objetivo: sempre visível
        return manager.IsConstructionUnlocked(data);
    }

    private static string GetKey(ConstructionData data) {
        if (data == null) return string.Empty;
        string key = !string.IsNullOrWhiteSpace(data.id) ? data.id : data.name;
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }
}