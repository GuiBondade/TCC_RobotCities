using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Slot
{
    public int currentTypeId;
    public bool hasTypeLocked;
    public int currentAmount;
    public int maxAmount;

    public void AddItems(int amount, int type) { // tem que ter algum return? (int excedente ou bool pra erro?)
        if (type != currentTypeId) {
            if ((currentTypeId != -1 || amount != 0/*não tem como um ser true sem o outro, entao sei la*/) || hasTypeLocked) { // se hasTypeLocked for true, currentTypeId vai ser imediatamente configurado como o tipo bloqueado não?
                return; // adicionou item de tipo errado(qtd != 0 importa? hasTypeLocked importa? na condição, porque pode ser redundante acho) (erro)
            } 
            currentTypeId = type;
        }
        int newAmount = currentAmount + amount; 
        if (newAmount > maxAmount) {
            return; // passou do máximo (erro)
            // da pra por só a quantidade certa e voltar o excedente sei la
        }
            
        amount = newAmount;
        return; //tudo certo
    }

    public void SubtractItems(int amount, int type) { // tem que ter algum return? (int excedente ou bool pra erro?)
        if (type != currentTypeId) {
            return; // não tem o item do tipo (erro)
        }
        int newAmount = currentAmount - amount; 
        if (newAmount <= 0) {
            return; // passou do mínimo (erro)
            // da pra retirar só a quantidade que tem e impedir passar de 0 sei la
        }

        if (newAmount == 0 && !hasTypeLocked) {
            currentTypeId = -1; //atualizar visual do slot? acho que a função que chama da reset no visual com os dados novos(ou seja, tem um middleware entre os dados e a parte visual, pra renderização correta e alterações)
        }
            
        amount = newAmount;
        return; //tudo certo
    }


}
