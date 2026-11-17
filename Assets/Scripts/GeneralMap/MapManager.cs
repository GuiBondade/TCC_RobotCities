using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    public static MapManager instancia { get; private set; }

    // dicionario de tiles ocupados
    public Dictionary<Vector3Int, IMapEntity> tilesOccupied = new Dictionary<Vector3Int, IMapEntity>();

    public List<BuildingController> allBuildings = new List<BuildingController>();
    public List<RobotScript> allRobots = new List<RobotScript>();
    public List<ResourceController> allResources = new List<ResourceController>();

    // Fonte única dos tilemaps do mapa principal.
    // Se ficar vazio, procura um MapLayers no próprio GameObject ou nos filhos.
    [Header("Tilemaps")]
    [SerializeField] public MapLayers layers;

    [Header("Contextos de mapa")]
    [Tooltip("Contexto da MapScene. O mapa principal continua referenciado mesmo quando um puzzle está ativo.")]
    [SerializeField] public MapContext mainContext;

    private MapContext activeContext;

    // Disparado quando o mapa alvo de input/colocação muda (puzzle aberto/fechado).
    public event System.Action<MapContext> OnActiveContextChanged;

    public MapContext MainContext => mainContext;
    public MapContext ActiveContext => activeContext != null ? activeContext : mainContext;
    public MapLayers ActiveLayers => ActiveContext != null && ActiveContext.layers != null
        ? ActiveContext.layers
        : layers;

    // Seções de import de entidades: os mesmos nomes usados nos blocos de import da workspace.
    public const string SectionRobot = "Robo";
    public const string SectionConstruction = "Construcao";
    public const string SectionScript = "Script";
    
    private void Awake() {
        if (instancia != null && instancia != this) {
            Destroy(gameObject);
            return;
        }
        instancia = this;

        // Resolve sem depender da ordem de Awake dos outros scripts.
        if (layers == null) layers = GetComponentInChildren<MapLayers>(true);
        if (mainContext == null) mainContext = GetComponentInChildren<MapContext>(true);
        if (mainContext == null) mainContext = FindObjectOfType<MapContext>();
        if (layers == null && mainContext != null) layers = mainContext.layers;
    }

    private void OnDestroy() {
        if (instancia == this) instancia = null;
        OnActiveContextChanged = null;
    }

    // Troca o mapa usado por input/colocação (PlacementManager). Passar null restaura
    // o contexto principal. O mapa principal continua existindo e rodando.
    public void SetActiveContext(MapContext context) {
        if (activeContext == context) return;
        activeContext = context;
        OnActiveContextChanged?.Invoke(ActiveContext);
    }

    public void ClearActiveContext() {
        SetActiveContext(null);
    }
    
    // Normaliza o nome da seção (aceita variações de escrita/capitalização/acento).
    public static string NormalizeSection(string section) {
        if (string.IsNullOrEmpty(section)) return SectionRobot;
        string s = RemoveAccents(section.Trim().ToLower());
        if (s.Contains("constr") || s.Contains("build") || s.Contains("edif")) return SectionConstruction;
        if (s.Contains("robo") || s.Contains("robot")) return SectionRobot;
        if (s.Contains("script")) return SectionScript;
        return section.Trim();
    }
    
    // Remove acentos pra comparar "Robô" e "Robo" como a mesma coisa.
    private static string RemoveAccents(string text) {
        if (string.IsNullOrEmpty(text)) return text;
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (char c in normalized) {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
    
    // Entidades de uma seção (na ordem das listas do mapa). Serve pra popular o dropdown do bloco de import.
    // Seção vazia/null = todas as entidades (robôs + construções).
    public List<IMapEntity> GetEntitiesBySection(string section) {
        var result = new List<IMapEntity>();
        if (instancia == null) return result;
        
        if (string.IsNullOrEmpty(section))
        {
            AddRobots(result);
            AddBuildings(result);
            return result;
        }
        
        string sec = NormalizeSection(section);
        switch (sec) {
            case SectionConstruction:
                AddBuildings(result);
                break;
            case SectionScript:
                // Scripts não são entidades do mapa: a lista fica vazia
                // (o alvo "Script" do bloco de import usa o ScriptManager).
                break;
            default:
                AddRobots(result);
                AddBuildings(result);
                break;
        }
        return result;
    }
    
    private void AddRobots(List<IMapEntity> target) {
        if (instancia == null || instancia.allRobots == null) return;
        foreach (var r in instancia.allRobots) if (r != null) target.Add(r);
    }
    
    private void AddBuildings(List<IMapEntity> target) {
        if (instancia == null || instancia.allBuildings == null) return;
        foreach (var b in instancia.allBuildings) if (b != null) target.Add(b);
    }
    
    // Procura a entidade pela seção + id (instance id) e/ou pelo nome.
    // Prioridade: instanceId; depois nome; depois índice na lista da seção.
    public IMapEntity FindEntity(string section, int instanceId, string entityName) {
        if (instancia == null) return null;
        
        var candidates = GetEntitiesBySection(section);
        
        if (instanceId != 0)
        {
            foreach (var e in candidates)
            {
                if (e == null) continue;
                var mb = e as MonoBehaviour;
                if (mb != null && mb.gameObject.GetInstanceID() == instanceId) return e;
            }
        }
        
        if (!string.IsNullOrEmpty(entityName))
        {
            foreach (var e in candidates)
            {
                if (e == null) continue;
                if (e.EntityName == entityName) return e;
            }
            
            // Fallback: o texto pode ser o índice (posição) da entidade na seção.
            if (int.TryParse(entityName.Trim(), out int index) && index >= 0 && index < candidates.Count)
                return candidates[index];
        }
        
        return null;
    }
    
    // Índice (posição) da entidade na sua seção. Usado pelo bloco de import pra
    // salvar uma referência estável mesmo quando o nome/número muda.
    public int GetEntityIndex(string section, IMapEntity entity) {
        if (instancia == null || entity == null) return -1;
        var candidates = GetEntitiesBySection(section);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (ReferenceEquals(candidates[i], entity)) return i;
        }
        return -1;
    }
    
    // Nome de exibição da entidade na lista do bloco de import.
    // Nomes repetidos ganham o instance id como sufixo (pra não colidir na lista).
    public static string GetEntityDisplayName(List<IMapEntity> entities, IMapEntity entity) {
        if (entity == null) return string.Empty;
        string entityName = entity.EntityName;
        if (string.IsNullOrEmpty(entityName)) return string.Empty;
        if (entities == null) return entityName;

        int sameNameCount = 0;
        foreach (var e in entities)
        {
            if (e == null) continue;
            if (e.EntityName == entityName) sameNameCount++;
        }
        if (sameNameCount <= 1) return entityName;
        return $"{entityName} ({entity.GetEntityId()})";
    }
    
    // Procura a entidade pelo texto exibido na lista do bloco de import.
    public IMapEntity FindEntityByDisplayName(string section, string displayName) {
        if (instancia == null || string.IsNullOrEmpty(displayName)) return null;

        var candidates = GetEntitiesBySection(section);
        foreach (var e in candidates)
        {
            if (e == null) continue;
            if (GetEntityDisplayName(candidates, e) == displayName) return e;
        }
        return null;
    }
    // fun��ǜo de checar ocupa��ǜo de um tile. Considera apenas as entidades registradas
    // (constru����es/recursos/rob��s). O overlay ("TilesFixos": rio, montanha
    // diagonal, farlands...) Ǹ puramente visual e nǜo ocupa cǸlulas; o bloqueio
    // por terreno (ǭgua/montanha) vem do chǜo (TerrainTile).
    public bool IsTileOccupied(Vector3Int tile) {
        return IsTileOccupied(tile, layers);
    }

    // Variante para mapas com contexto próprio (puzzle): usa o overlay do mapa certo,
    // sem consultar os tilemaps do mapa principal.
    //
    // O overlay ("TilesFixos": rio, montanha diagonal, farlands...) é puramente
    // visual: não ocupa células. O bloqueio vem do terreno do chão (água/montanha
    // via TerrainTile) e das entidades registradas (construções/recursos/robôs).
    // Por isso o overlay não é considerado aqui — construções andáveis como a
    // ponte podem ser colocadas sobre o rio, porque a célula de água passa no
    // teste de terreno da própria construção.
    public bool IsTileOccupied(Vector3Int tile, MapLayers mapLayers) {
        IMapEntity occupiedTile;
        if (tilesOccupied.TryGetValue(tile, out occupiedTile) && occupiedTile != null) return true;
        return false;
    }

    // Nome único no projeto em execução: considera robôs, construções e recursos,
    // inclusive entidades de mapas aditivos já carregados. Entidades autorais de cenas
    // não carregadas não entram aqui (validação de projeto fica numa ferramenta à parte).
    public string GetUniqueEntityName(string desiredName, IMapEntity ignore = null) {
        var names = new List<string>();
        AddEntityNames(names, ignore);
        return StringUtils.EnsureUniqueString(desiredName, names, "Entidade");
    }

    public bool IsEntityNameInUse(string entityName, IMapEntity ignore = null) {
        if (string.IsNullOrEmpty(entityName)) return false;

        foreach (var robot in allRobots) {
            if (robot == null || ReferenceEquals(robot, ignore)) continue;
            if (string.Equals(robot.EntityName, entityName, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        foreach (var building in allBuildings) {
            if (building == null || ReferenceEquals(building, ignore)) continue;
            if (string.Equals(building.EntityName, entityName, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        foreach (var resource in allResources) {
            if (resource == null || ReferenceEquals(resource, ignore)) continue;
            if (string.Equals(resource.EntityName, entityName, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void AddEntityNames(List<string> names, IMapEntity ignore) {
        if (allRobots != null) {
            foreach (var robot in allRobots) {
                if (robot == null || ReferenceEquals(robot, ignore)) continue;
                names.Add(robot.EntityName);
            }
        }
        if (allBuildings != null) {
            foreach (var building in allBuildings) {
                if (building == null || ReferenceEquals(building, ignore)) continue;
                names.Add(building.EntityName);
            }
        }
        if (allResources != null) {
            foreach (var resource in allResources) {
                if (resource == null || ReferenceEquals(resource, ignore)) continue;
                names.Add(resource.EntityName);
            }
        }
    }

    // função de retornar inventoryManager de um tile(checa se esta ocupado antes)
    // Caminho estrito: loga erro de debug quando o tile está livre ou quando a
    // entidade que ocupa o tile não tem inventário.
    public InventoryManager GetTileInventory(Vector3Int tile) {
        var entity = GetTileEntity(tile);
        if (entity == null) {
            Debug.LogError("[MapManager] Tile " + tile + " está livre: nenhuma entidade ocupa a célula.", this);
            return null;
        }

        var inventory = entity.GetInventoryManager();
        if (inventory == null) {
            Debug.LogError("[MapManager] Entidade '" + entity.EntityName + "' no tile " + tile + " não possui InventoryManager.", this);
            return null;
        }

        return inventory;
    }

    // Variante silenciosa: devolve false sem logar quando não há inventário no tile.
    // Usada nos fluxos em que "não há container no vizinho" é uma condição normal
    // do jogo (ex.: guardar item num tile sem armazém).
    public bool TryGetTileInventory(Vector3Int tile, out InventoryManager inventory) {
        inventory = null;

        var entity = GetTileEntity(tile);
        if (entity == null) return false;

        inventory = entity.GetInventoryManager();
        return inventory != null;
    }

    // Entidade que ocupa o tile (null se o tile está livre).
    public IMapEntity GetTileEntity(Vector3Int tile) {
        if (tilesOccupied.TryGetValue(tile, out var entity)) return entity;
        return null;
    }

    // Recurso extraível que ocupa o tile (null se o tile está livre ou tem outra entidade
    // não extraível, ex.: armazém/fábrica). O bloco ExtrairMaterial usa este caminho:
    // tile -> IMapEntity -> IExtractable, mas só retorna fontes com CanExtract == true
    // (recurso com estoque / construção ativa e configurada como fonte).
    public IExtractable GetTileExtractable(Vector3Int tile) {
        var extractable = GetTileEntity(tile) as IExtractable;
        return (extractable != null && extractable.CanExtract) ? extractable : null;
    }

    // Recurso extraível na célula vizinha (célula de origem + direção, 1 tile).
    public IExtractable GetAdjacentExtractable(Vector3Int fromCell, Vector2Int direction) {
        return GetTileExtractable(fromCell + new Vector3Int(direction.x, direction.y, 0));
    }

    // função de limpar um tile, desocupando
    public void DesoccupyTile(Vector3Int tile) {
        tilesOccupied.Remove(tile);
    }

    // função de ocupar um tile com algo passado como parametro
    public void OccupyTile(Vector3Int tile, IMapEntity mapEntity) {
        tilesOccupied[tile] = mapEntity;
    }

    // registra uma entidade no mapa: ocupa as células e a guarda na lista do seu tipo.
    // O nome é tornado único antes de entrar na lista (robôs, construções e recursos
    // compartilham o mesmo espaço de nomes).
    public void RegisterEntity(IMapEntity entity, IEnumerable<Vector3Int> positions = null) {
        if (entity == null) return;

        bool alreadyRegistered = IsRegistered(entity);
        if (!alreadyRegistered) {
            string currentName = entity.EntityName;
            string uniqueName = GetUniqueEntityName(currentName, entity);
            if (entity is MapEntityBase mapEntity && !string.Equals(currentName, uniqueName, System.StringComparison.Ordinal)) {
                mapEntity.SetEntityName(uniqueName);
            }
        }

        if (positions != null) {
            foreach (var pos in positions) tilesOccupied[pos] = entity;
        }

        if (entity is BuildingController building && !allBuildings.Contains(building)) allBuildings.Add(building);
        if (entity is ResourceController resource && !allResources.Contains(resource)) allResources.Add(resource);
        if (entity is RobotScript robot && !allRobots.Contains(robot)) allRobots.Add(robot);
    }

    private bool IsRegistered(IMapEntity entity) {
        if (entity is BuildingController building) return allBuildings.Contains(building);
        if (entity is ResourceController resource) return allResources.Contains(resource);
        if (entity is RobotScript robot) return allRobots.Contains(robot);
        return false;
    }

    // remove uma entidade do mapa: libera as células ocupadas e tira das listas
    public void RemoveEntity(IMapEntity entity) {
        // ReferenceEquals (e não ==): um objeto já destruído pelo Unity continua
        // sendo referência válida aqui, e as células dele ainda precisam ser liberadas.
        if (ReferenceEquals(entity, null)) return;

        var occupiedByEntity = tilesOccupied
            .Where(kv => ReferenceEquals(kv.Value, entity))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var pos in occupiedByEntity) tilesOccupied.Remove(pos);

        if (entity is BuildingController building) allBuildings.Remove(building);
        if (entity is ResourceController resource) allResources.Remove(resource);
        if (entity is RobotScript robot) allRobots.Remove(robot);
    }
}
