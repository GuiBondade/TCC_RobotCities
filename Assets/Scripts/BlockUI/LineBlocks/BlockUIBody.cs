using UnityEngine;
using UnityEngine.UI;

public class BlockUIBody : BlockUI
{
    public BlockSlot slotBody;
    public RectTransform bodySpacer;
    
    public Image backgroundSecondary;

    public override void SetupUI(BlockData newData) {
        base.SetupUI(newData);

        if (backgroundSecondary != null){
            backgroundSecondary.color = newData.color;
            bodySpacer.GetComponent<Image>().color = newData.color;
        }
    }

// ------------ Acessores ------------

    public override BlockUI GetBody() { 
        if (slotBody == null || slotBody.currentBlock == null) return null;
        else return slotBody.currentBlock;
    }

    public override float GetBodySpacerHeight(float total)
    {
        total += bodySpacer.rect.height + slotNext.slotRect.rect.height; 
        // tamanho adicional dos filhos do slot body
        // espaço do slot next
        return total;
    }

// ------------ Helpers para spacer ------------
    public override void ResetSpacer()
    {
        // if (bodySpacer == null) return;
        bodySpacer.sizeDelta = new Vector2(bodySpacer.sizeDelta.x, defaultSpacerHeight);
    }
}
