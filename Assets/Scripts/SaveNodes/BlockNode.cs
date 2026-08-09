using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BlockNode
{
   public int id;
   public string functionId; //= função (andar, esperar, if, etc)
   public string variableNameReference; // caso seja set de variavel
   public List<int> paramIds; // tinha que ver como pegar o parametro de valor default quando for criar_variavel
   public int rootId; // referencia pro bloco inicial da linha, se for o mesmo que o id e for bloco de inicio de linha tipo start ou repetir sempre ou criar variavel ou outro, dai pode executar a linha dele (ai ve se, caso não for, traduz para lista de execução ou não)
   public int next; // id do próximo bloco
   public int body; // id do bloco dentro (loop/if)
   public int elseBlockId; // dava pra trocar todos esses id de bloco em sequencia por uma lista, onde 0 é next, 1 é body e outros indexes são else ou cases de switch sei la
}