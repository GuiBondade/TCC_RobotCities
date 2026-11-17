using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SlotInterface : MonoBehaviour
{
    public Image icone; // alterar sprite pelo do material/ferramenta em questão do tipo
    // component EventTrigger na image, detecta entrada, ativando GO do tooltipLabel e desativando ao sair
    public TMP_Text tooltipLabel; // alterar text pra expor nome do material/ferramenta no hover
    public TMP_Text amountLabel; // alterar text pra expor quantidade/usos

    public void UpdateSlot(int amount, MaterialData material) {
        UpdateMaterialSlot(amount, material);
    }

    public void UpdateMaterialSlot(int amount, MaterialData material) {
        if (icone != null) icone.sprite = material.icon;
        if (amountLabel != null) amountLabel.text = amount.ToString();
        if (tooltipLabel != null) tooltipLabel.text = material.itemName;
    }

    public void UpdateToolSlot(ToolData tool, int uses, int maxUses) {
        if (tool == null || uses <= 0) {
            ShowEmpty();
            return;
        }
        if (icone != null) icone.sprite = tool.icon;
        if (amountLabel != null) amountLabel.text = (maxUses > 0 ? uses + "/" + maxUses : uses.ToString());
        if (tooltipLabel != null) tooltipLabel.text = tool.displayName;
    }

    public void ShowEmpty() {
        if (icone != null) icone.sprite = null;
        if (amountLabel != null) amountLabel.text = "";
        if (tooltipLabel != null) tooltipLabel.text = "";
    }
}
