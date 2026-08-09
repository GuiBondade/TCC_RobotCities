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
