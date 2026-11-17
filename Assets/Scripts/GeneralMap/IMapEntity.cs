using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Entidade que ocupa o mapa (construção, robô ou recurso).
// A implementação comum (id, signals e variáveis expostas) fica no MapEntityBase;
// a interface existe pra quem consome (MapManager, ExecutionManager, blocos de import)
// não depender do tipo concreto.
public interface IMapEntity {
    string EntityName { get; }
    InventoryManager GetInventoryManager();
    
    // Identificador único para scoping por entidade (instance id do GameObject)
    int GetEntityId();
    
    // Signals expostos pela entidade (únicos entre as entidades)
    IReadOnlyList<string> GetExposedSignals();
    
    // Verifica se o signal está marcado para trigger (edge-trigger via bool)
    bool GetSignalBool(string signalName);
    void SetSignalBool(string signalName, bool value);
    void ClearSignalBool(string signalName);
    
    // Dispara o signal para contextos que importaram (entity, signal) exato
    void TriggerEntitySignal(string signalName);
    
    // Variáveis expostas pela entidade - retorna referência direta/gerenciada para a variável
    // Retorna true se a variável foi encontrada e a referência foi atribuída
    bool TryGetExposedVariable(string varName, out object varRef);
    IReadOnlyList<string> GetExposedVariables();

    // Variáveis do script que a entidade executa (exportadas por esse script).
    // Quem importa acessa em modo somente leitura.
    IReadOnlyList<string> GetScriptVariableNames();
    object GetScriptVariableValue(string varName);
}
