using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LayoutOption : MonoBehaviour
{
    public Image icone;
    public Button button;
    public TMP_Text nome;
    public Color corSaturada;
    public Color corDessaturada;

    // na verdade ser uma linha horizontal com os sprites de cada slot e quantidade por craft (input/output)

    public void ActivateLayout(){
        icone.color = corSaturada;// saturar pro padrão
        button.interactable = true;// ativar interação de clique
    }

    public void DeactivateLayout() {
        icone.color = corDessaturada;// dessaturar
        button.interactable = false;// desativar interação de clique
    }
}
