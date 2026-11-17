using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Balões de debug do robô: instancia o DebugPrefab sob o DebugContainer.
public class RobotDebug : MonoBehaviour
{
    [Tooltip("Prefab do balão de debug (DebugPrefab).")]
    [SerializeField] public GameObject debugPrefab;

    [Tooltip("Máximo de balões no container. Ao atingir, o mais antigo é removido. <= 0 = sem limite.")]
    [SerializeField] public int maxBaloes = 5;

    // Balões criados, na ordem de criação (base para limite/opacidade nos próximos prompts).
    private readonly List<GameObject> baloes = new List<GameObject>();
    public IReadOnlyList<GameObject> Baloes => baloes;

    // Instancia um balão com o texto informado e o deixa como filho deste container.
    // Retorna null (sem instanciar) se o texto for nulo ou vazio.
    public GameObject DebugSpawner(string texto) {
        if (string.IsNullOrEmpty(texto)) return null;

        if (debugPrefab == null) {
            Debug.LogError("[RobotDebug] debugPrefab não configurado no DebugContainer.", this);
            return null;
        }

        LimitarBaloes(); // libera espaço antes de nascer o próximo

        GameObject balao = Instantiate(debugPrefab, transform);
        balao.name = "DebugPrefab";

        var balloon = balao.GetComponent<DebugBalloon>();
        if (balloon != null) {
            balloon.DefinirTexto(texto);
        } else {
            var label = balao.GetComponentInChildren<TMP_Text>(true);
            if (label != null) {
                label.text = texto;      // texto do bloco (1 linha) ou "nome: valor" da variável
                label.ForceMeshUpdate(); // preferred size correto antes do rebuild do layout
            } else {
                Debug.LogError("[RobotDebug] O DebugPrefab não possui TMP_Text.", balao);
            }
        }

        baloes.Add(balao);

        // VerticalLayoutGroup + ContentSizeFitter do DebugContainer recalculam já neste
        // frame: o balão nasce com as 2 linhas e o container se expande junto.
        if (transform is RectTransform container) LayoutRebuilder.ForceRebuildLayoutImmediate(container);

        return balao;
    }

    // Remove os balões mais antigos (primeiros da hierarquia) até caber mais um.
    private void LimitarBaloes() {
        baloes.RemoveAll(b => b == null); // limpa referências de balões que expiraram
        if (maxBaloes <= 0) return;

        // +1 = o balão que ainda vai nascer.
        while (transform.childCount + 1 > maxBaloes) {
            if (transform.childCount == 0) break;

            GameObject antigo = transform.GetChild(0).gameObject;
            baloes.Remove(antigo);

            // Destroy é adiado pro fim do frame: desanexar já mantém o childCount correto
            // (senão este while nunca terminaria). SetActive dispara o OnDisable do balão,
            // que sai do tick na hora.
            antigo.SetActive(false);
            antigo.transform.SetParent(null, false);
            Destroy(antigo);
        }
    }

    [ContextMenu("Testar DebugSpawner")]
    private void TestarDebugSpawner() {
        if (!Application.isPlaying) return;
        DebugSpawner("Teste de debug\n(linha 2)");
    }
}
