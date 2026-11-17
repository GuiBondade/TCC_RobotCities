using System.Collections.Generic;
using UnityEngine;

// Opção mostrada no painel de escolha de entidade.
public class EntitySelectionOption
{
    public string Label;
    public System.Action OnSelected;
}

// Resolve o clique em um ponto do mapa considerando TODOS os colliders 2D
// atingidos, não só o que está visualmente na frente. Assim, uma entidade
// coberta por outra ainda pode ser acessada pelo painel de seleção.
public static class EntitySelectionResolver
{
    private static readonly List<EntitySelectionOption> s_Options = new List<EntitySelectionOption>();
    private static readonly List<int> s_SeenTargets = new List<int>();

    public static void ResolveClick(Vector2 screenPosition) {
        // Clique em cima do próprio painel: quem trata é o botão da UI.
        if (EntitySelectionPanel.IsPointerOverPanel(screenPosition)) return;

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        Vector2 point = new Vector2(worldPosition.x, worldPosition.y);

        Collider2D[] overlaps = Physics2D.OverlapPointAll(point);

        s_Options.Clear();
        s_SeenTargets.Clear();

        foreach (Collider2D collider in overlaps) {
            if (collider == null) continue;

            TriggerInterfaceBase trigger = collider.GetComponentInParent<TriggerInterfaceBase>();
            if (trigger == null) trigger = collider.GetComponentInChildren<TriggerInterfaceBase>(true);
            if (trigger == null) continue;

            MonoBehaviour target = trigger.TargetScript;
            if (target == null) continue;

            int targetId = target.GetInstanceID();
            if (s_SeenTargets.Contains(targetId)) continue;
            s_SeenTargets.Add(targetId);

            TriggerInterfaceBase capturedTrigger = trigger;
            s_Options.Add(new EntitySelectionOption {
                Label = GetEntityName(target),
                OnSelected = () => {
                    if (capturedTrigger == null) return;
                    capturedTrigger.OpenTarget(screenPosition);
                }
            });
        }

        if (s_Options.Count == 0) return;

        if (s_Options.Count == 1) {
            EntitySelectionPanel.HideCurrent();
            s_Options[0].OnSelected?.Invoke();
            return;
        }

        EntitySelectionPanel panel = EntitySelectionPanel.Instance;
        if (panel == null) {
            // Sem painel configurado na cena: não perde o clique, abre a primeira opção.
            Debug.LogWarning("[EntitySelection] Nenhum EntitySelectionPanel encontrado na cena. " +
                "Adicione o componente no painel de dynamic interfaces.", null);
            s_Options[0].OnSelected?.Invoke();
            return;
        }

        panel.Show(s_Options, screenPosition);
    }

    private static string GetEntityName(MonoBehaviour target) {
        if (target is IMapEntity entity && !string.IsNullOrEmpty(entity.EntityName)) {
            return entity.EntityName;
        }

        return target.gameObject.name;
    }
}
