using UnityEngine;

// Entidade do mapa que pode fornecer material (recurso do mapa ou construção-fonte,
// ex.: mina). Implementado pelo ResourceController (estoque finito que esgota) e
// pelo BuildingController quando o ConstructionData marcar a construção como fonte
// de extração (caso da Mina de Pedra, que é infinita e nunca se autodestroi).
public interface IExtractable {
    // A fonte está disponível para extração agora?
    // (recurso com estoque restante / construção ativa e configurada como fonte)
    bool CanExtract { get; }

    // Material devolvido pela extração (id do MaterialDatabase).
    int MaterialTypeId { get; }

    // Tipo do recurso: define com qual ferramenta ele pode ser extraído.
    ResourceType ResourceType { get; }

    // Retira material do recurso e devolve quanto foi extraído (0 se nada saiu).
    int ExtractResource(int amount);

    // Transação: tira do recurso e põe no inventário de destino.
    // Devolve quanto foi transferido (0 se não deu pra transferir).
    int ExtractTo(InventoryManager target, int amount);
}
