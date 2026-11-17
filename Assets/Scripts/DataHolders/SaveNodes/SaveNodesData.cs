using UnityEngine;
using System.Collections.Generic;

// Variáveis locais de uma função: ligadas ao DeclareFunction dono pelo id do bloco.
[System.Serializable]
public class FunctionLocalVars
{
   public int functionBlockId; // id do BlockNode do DeclareFunction
   public List<CreateVarNode> vars = new();
}

[System.Serializable]
public class SaveNodesData
{
   public List<ParameterNode> parametrosData = new();
   public List<BlockNode> blocosData = new();
   public List<CreateVarNode> variaveisData = new(); // variáveis globais (do workspace)
   public List<FunctionLocalVars> functionLocals = new(); // variáveis locais por função

   public void Clear()
   {
      parametrosData.Clear();
      blocosData.Clear();
      variaveisData.Clear();
      functionLocals.Clear();
   }
}