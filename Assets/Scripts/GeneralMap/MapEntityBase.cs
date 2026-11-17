using System.Collections.Generic;
using UnityEngine;

// Base das entidades que ocupam o mapa (construção, robô e recurso).
// Fica aqui o que é igual nas três: identificador de instância, signals expostos
// e variáveis expostas. O que muda de tipo pra tipo (nome, inventário e
// variáveis de script) fica nas classes filhas.
public abstract class MapEntityBase : MonoBehaviour, IMapEntity
{
    // Listas serializadas pra poderem ser editadas no Inspector.
    [SerializeField] private List<string> exposedSignalsList = new List<string>();
    [SerializeField] private List<string> exposedVariablesList = new List<string>();

    private Dictionary<string, bool> signalBools = new Dictionary<string, bool>();
    private Dictionary<string, object> exposedVarRefs = new Dictionary<string, object>();

    // Nome que aparece nos blocos de import (cada entidade tem a sua fonte).
    public abstract string EntityName { get; }

    // Aplica um nome único decidido pelo MapManager. Cada entidade grava na própria
    // fonte (RobotData, displayName ou nome exibido do recurso).
    public virtual void SetEntityName(string entityName) {
        if (string.IsNullOrEmpty(entityName)) return;
        gameObject.name = entityName;
    }

    // Inventário da entidade (nem toda entidade tem: o robô pega do RobotInventory).
    public abstract InventoryManager GetInventoryManager();

    // Identificador único para scoping por entidade (instance id do GameObject).
    public virtual int GetEntityId() => gameObject.GetInstanceID();

    // Signals expostos pela entidade (únicos entre as entidades).
    public IReadOnlyList<string> GetExposedSignals() => exposedSignalsList;

    // Verifica se o signal está marcado para trigger (edge-trigger via bool).
    public bool GetSignalBool(string signalName)
    {
        if (string.IsNullOrEmpty(signalName)) return false;
        signalBools.TryGetValue(signalName, out bool val);
        return val;
    }

    public void SetSignalBool(string signalName, bool value)
    {
        if (string.IsNullOrEmpty(signalName)) return;
        signalBools[signalName] = value;
        if (value)
        {
            // Edge-trigger: ao setar true, dispara para quem importou
            if (ExecutionManager.instancia != null)
                ExecutionManager.instancia.TriggerEntitySignalFromEntity(this, signalName);
        }
    }

    public void ClearSignalBool(string signalName)
    {
        if (string.IsNullOrEmpty(signalName)) return;
        signalBools[signalName] = false;
    }

    // Dispara o signal para contextos que importaram (entity, signal) exato.
    public void TriggerEntitySignal(string signalName)
    {
        if (ExecutionManager.instancia != null)
            ExecutionManager.instancia.TriggerEntitySignalFromEntity(this, signalName);
    }

    // Variáveis expostas pela entidade: referência direta/gerenciada da variável.
    // Devolve true se achou a variável (e preenche varRef).
    public bool TryGetExposedVariable(string varName, out object varRef)
    {
        varRef = null;
        if (string.IsNullOrEmpty(varName)) return false;
        if (exposedVarRefs.TryGetValue(varName, out varRef)) return true;
        return false;
    }

    public IReadOnlyList<string> GetExposedVariables() => exposedVariablesList;

    // Variáveis do script que a entidade executa (exportadas por esse script).
    // Só quem executa script (o robô) sobrescreve: por padrão não expõe nenhuma.
    public virtual IReadOnlyList<string> GetScriptVariableNames() => System.Array.Empty<string>();

    public virtual object GetScriptVariableValue(string varName) => null;

    public void RegisterExposedVariable(string varName, object varRef)
    {
        if (string.IsNullOrEmpty(varName)) return;
        exposedVarRefs[varName] = varRef;
        if (!exposedVariablesList.Contains(varName)) exposedVariablesList.Add(varName);
    }

    public void RegisterExposedSignal(string signalName)
    {
        if (string.IsNullOrEmpty(signalName)) return;
        if (!exposedSignalsList.Contains(signalName)) exposedSignalsList.Add(signalName);
        if (!signalBools.ContainsKey(signalName)) signalBools[signalName] = false;
    }
}
