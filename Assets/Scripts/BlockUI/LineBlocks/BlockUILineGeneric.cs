using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BlockUI : MonoBehaviour
{
    public Image background;
    public TMP_Text label;

    [HideInInspector] public BlockData data;
    public RectTransform TopSlot;
    public List<ReferenceHolder> parameterInitialList;

    // Rótulos visuais dos parâmetros (criados em runtime; destruídos no cleanup do bloco).
    [HideInInspector] public List<GameObject> parameterLabels = new List<GameObject>();
    [Tooltip("Escala do rótulo do parâmetro em relação à fonte do nome do bloco.")]
    public float paramLabelFontScale = 0.65f;
    [Tooltip("Cor dos rótulos de parâmetro.")]
    public Color paramLabelColor = new Color(0f, 0f, 0f, 0.7f);
    public RectTransform blockRect;
    public BlockSlot slotNext;

    public List<BlockUI> bodyAncestors = new List<BlockUI>();
    public BlockUI parentBodyOwner;

    public float defaultSpacerHeight = 30f;
    public float defaultSpacerWidth = 30f;

    public virtual void SetupUI(BlockData newData) {
        data = newData;

        if (label != null) {
            label.text = newData.blockName;
            label.ForceMeshUpdate();
        }
        if (background != null) background.color = newData.color;
    }

    // Rótulo configurado no BlockData para o parâmetro na posição (null = sem rótulo).
    public string GetParameterLabel(int index) {
        if (data == null || data.rotulosParametros == null) return null;
        if (index < 0 || index >= data.rotulosParametros.Count) return null;
        return string.IsNullOrEmpty(data.rotulosParametros[index]) ? null : data.rotulosParametros[index];
    }

    // Cria o rótulo visual do parâmetro antes dele no TopSlot.
    // Se houver um prefab referenciado (BlocksFactory.paramLabelPrefab), instancia-o (o usuário
    // controla fonte/estilo/margens no Editor); senão cai no fallback criado em código.
    // Retorna null se não houver texto ou TopSlot.
    public GameObject CreateParameterLabel(string texto) {
        if (string.IsNullOrEmpty(texto) || TopSlot == null) return null;

        GameObject go = null;

        // Caminho preferido: prefab definido pelo usuário (BlocksFactory.paramLabelPrefab).
        var prefab = BlocksFactory.instance != null ? BlocksFactory.instance.paramLabelPrefab : null;
        if (prefab != null) {
            go = Instantiate(prefab, TopSlot, false);
            go.name = "ParamLabel";
        } else {
            // Fallback: cria um TMP em código (mantém compatibilidade se o prefab não estiver setado).
            go = new GameObject("ParamLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(TopSlot, false);

            var tmpFb = go.GetComponent<TextMeshProUGUI>();
            tmpFb.raycastTarget = false;
            tmpFb.enableWordWrapping = false;
            tmpFb.alignment = TextAlignmentOptions.Center;
            if (label != null) {
                tmpFb.font = label.font;
                tmpFb.alignment = label.alignment;
                float escala = paramLabelFontScale > 0f ? paramLabelFontScale : 0.65f;
                tmpFb.fontSize = Mathf.Max(1f, label.fontSize * escala);
                tmpFb.fontStyle = label.fontStyle;
            }
            tmpFb.color = paramLabelColor;
        }

        // Preenche o texto (padronizado com o debug: "rótulo:").
        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) {
            tmp.text = texto + ":";
            tmp.ForceMeshUpdate();
        }

        parameterLabels.Add(go);
        return go;
    }

    // Remove os rótulos criados (o bloco volta pro pool).
    public void ClearParameterLabels() {
        if (parameterLabels == null) parameterLabels = new List<GameObject>();
        for (int i = 0; i < parameterLabels.Count; i++) {
            if (parameterLabels[i] == null) continue;
            // Some já neste frame; o Destroy efetivo fica pro fim do frame.
            parameterLabels[i].SetActive(false);
            Destroy(parameterLabels[i]);
        }
        parameterLabels.Clear();
    }

// ------------ Acessores ------------

    // Retorna referência ao próximo bloco no slotNext ou slotBody
    public BlockUI GetNext() { 
        if (slotNext == null || slotNext.currentBlock == null) return null;
        else return slotNext.currentBlock;
    }

    public virtual BlockUI GetBody() { 
        return null;
    }

    // pega o ultimo bloco da linha
    public BlockUI GetTail() {
        BlockUI tail = this;
        while (tail.GetNext() != null) {
            tail = tail.GetNext();
        }
        return tail;
    }

    // Retorna a altura total ocupada por este bloco e toda a cadeia abaixo (next/body),
    // incluindo os bodySpacers dos blocos que possuem body.
    public float GetTailHeight()
    {
        float total = 0f;

        if (blockRect != null) total += blockRect.rect.height;

        total = GetBodySpacerHeight(total);

        // soma recursiva do next
        var next = GetNext();
        if (next != null)
            total += next.GetTailHeight();
        //Debug.Log($"Calculando GetTailHeight para {this}: altura total = {total}");
        return total;
    }

    public virtual float GetBodySpacerHeight(float total) 
    {
        return total;
    }

// ------------ Ancestralidade ------------

    public void AssignBodyAncestorsRecursive(List<BlockUI> parentAncestors, BlockUI parentBodyOwner)
    {
        // Atualiza lista local
        bodyAncestors.Clear();
        if (parentAncestors != null && parentAncestors.Count > 0)
            bodyAncestors.AddRange(parentAncestors);

        if (parentBodyOwner != null)
            bodyAncestors.Add(parentBodyOwner);

        this.parentBodyOwner = parentBodyOwner;

        // Propaga para next (mesmos ancestrais)
        var next = GetNext();
        if (next != null)
            next.AssignBodyAncestorsRecursive(this.bodyAncestors, null);

        // Propaga para body (agora o current block vira parentBodyOwner para os filhos do body)
        var bodyChild = GetBody();
        if (bodyChild != null)
        {
            bodyChild.AssignBodyAncestorsRecursive(this.bodyAncestors, this);
        }
    }

// ------------ Helpers para spacer ------------
    public virtual void ResetSpacer()
    {
        return;
    }

    // Ajusta todos os bodySpacers dos ancestors adicionando delta (pode ser positivo ou negativo)
    public void AdjustBodySpacers(float delta)
    {
        if (bodyAncestors == null || bodyAncestors.Count == 0) return;

        foreach (var ancestor in bodyAncestors) 
        {
            if (ancestor == null || ancestor is not BlockUIBody ancestorBody) continue;

            ancestorBody.bodySpacer.sizeDelta += new Vector2(0f, delta);
        }

        // Agora força o rebuild do layout apenas nos ancestors afetados
        foreach (var ancestor in bodyAncestors)
        {
            if (blockRect != null) 
                LayoutRebuilder.ForceRebuildLayoutImmediate(blockRect);
        }
    }
}
