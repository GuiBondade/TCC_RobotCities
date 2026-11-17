using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
//using UnityEngine.EventSystems; */
using TMPro;

public enum ParameterSections {
    BoolValues, //
    BoolAllComparisonOperations,
    BoolIntComparisonOperations,
    BoolLogicOperations,
    IntValues, //
    IntOperations,
    DirectionValues, //
    MaterialValues, //
    FunctionValues, // opções dinâmicas: lista viva de funções do script (ExecutionManager)
    SignalValues, // sentinel "Digitar..." (índices serializados nos assets/saves)
    // --- Bloco de import (listas dinâmicas: o valor vem do provider do bloco) ---
    Robos,        // instâncias de robô (MapManager)
    Construcoes,  // instâncias de construção (MapManager)
    Scripts,      // scripts do workspace (ScriptManager)
    Variaveis,    // variáveis do alvo (internas da entidade / do script do robô)
    Funcoes,      // funções exportadas do script de origem
    Signals,      // signals do alvo (entidade ou script)
    ToolValues,   // ferramentas (ToolDatabase)
    // --- Parâmetro de texto livre (bloco de debug) ---
    StringValues,   // sentinel "Digitar..." (texto livre, sem registrar signal)
    VariableValues,  // lista viva de todas as variáveis (qualquer tipo)
    // --- Parâmetro de entidade (operador booleano "contém") ---
    EntityGeneralValues,      // Nenhuma, Qualquer, Robô, Construção, Recurso
    EntityConstructionValues, // grupos de construção (EntityDatabase)
    EntityResourceValues,      // grupos de recurso (EntityDatabase)
    BoolEntityOperations      // seção do parâmetro booleano (operação "contém")
}

public enum ParameterType {
    BooleanParameter,
    AllTypeParameter,
    DirectionParameter,
    IntParameter,
    MaterialParameter,
    FunctionParameter, // opções dinâmicas (FunctionValues), como o SignalParameter
    SignalParameter, // sentinel "Digitar..." (índices serializados nos assets/saves)
    ImportTargetParameter,  // alvo do import (seções Robos/Construcoes/Scripts)
    ImportElementParameter, // o que importar (seções Variaveis/Funcoes/Signals)
    ToolParameter,         // ferramentas
    StringParameter,       // texto livre (input) ou variável — ao fim para preservar índices
    EntityParameter        // filtro de entidade (operador "contém") — ao fim para preservar índices
}

public class ParameterSetup : MonoBehaviour//, IIdentificadorScript
{    
    // Limite de caracteres do input livre quando o ParamData não define um (0 = sem limite).
    public const int LimiteCaracteresPadrao = 24;

    // Mapeamento tipo -> seção de valores (usado pela criação de variável — global e local).
    public static ParameterSections SectionForType(ParameterType type) {
        switch (type) {
            case ParameterType.BooleanParameter: return ParameterSections.BoolValues;
            case ParameterType.IntParameter: return ParameterSections.IntValues;
            case ParameterType.DirectionParameter: return ParameterSections.DirectionValues;
            case ParameterType.MaterialParameter: return ParameterSections.MaterialValues;
            case ParameterType.ToolParameter: return ParameterSections.ToolValues;
            case ParameterType.FunctionParameter: return ParameterSections.FunctionValues;
            case ParameterType.SignalParameter: return ParameterSections.SignalValues;
            case ParameterType.ImportTargetParameter: return ParameterSections.Robos;
            case ParameterType.ImportElementParameter: return ParameterSections.Variaveis;
            case ParameterType.StringParameter: return ParameterSections.StringValues;
            case ParameterType.EntityParameter: return ParameterSections.EntityGeneralValues;
            default: return ParameterSections.BoolValues;
        }
    }

    // Seções cujas opções são calculadas em runtime (o bloco de import monta a lista).
    // Não vêm do SectionDatabase: o ParameterSetup pula a busca estática nessas seções.
    // Rótulo em português para o tipo de parâmetro (usado no dropdown do declare).
    public static string ParameterTypeLabel(ParameterType type) {
        switch (type) {
            case ParameterType.BooleanParameter: return "Booleano";
            case ParameterType.AllTypeParameter: return "Todos os Tipos";
            case ParameterType.DirectionParameter: return "Direção";
            case ParameterType.IntParameter: return "Inteiro";
            case ParameterType.MaterialParameter: return "Material";
            case ParameterType.ToolParameter: return "Ferramenta";
            case ParameterType.FunctionParameter: return "Função";
            case ParameterType.SignalParameter: return "Signal";
            case ParameterType.ImportTargetParameter: return "Alvo de Import";
            case ParameterType.ImportElementParameter: return "Elemento de Import";
            case ParameterType.StringParameter: return "Texto";
            case ParameterType.EntityParameter: return "Entidade";
            default: return type.ToString();
        }
    }

    public static bool IsDynamicSection(ParameterSections section) {
        switch (section) {
            case ParameterSections.Robos:
            case ParameterSections.Construcoes:
            case ParameterSections.Scripts:
            case ParameterSections.Variaveis:
            case ParameterSections.Funcoes:
            case ParameterSections.Signals:
            case ParameterSections.VariableValues: // lista viva de variáveis (ParameterConfig.UpdateValues)
            case ParameterSections.EntityGeneralValues:
            case ParameterSections.EntityConstructionValues:
            case ParameterSections.EntityResourceValues:
                return true;
            default:
                return false;
        }
    }

    // Monta a lista dinâmica da seção [entidade] a partir do catálogo do SectionDatabase.
    private List<ParamValue> GetEntityValues(ParameterSections section) {
        if (sectionDatabase == null) {
            Debug.LogError("[Parametros] SectionDatabase não atribuído no ParameterSetup — seção de entidade vazia.", this);
            return new List<ParamValue>();
        }
        return sectionDatabase.GetEntityValues(section);
    }

    // Texto exibido no cabeçalho da seção do popup.
    public static string SectionLabel(ParameterSections section) {
        switch (section) {
            case ParameterSections.BoolValues: return "Valores booleanos";
            case ParameterSections.BoolAllComparisonOperations: return "Comparações booleanas";
            case ParameterSections.BoolIntComparisonOperations: return "Comparações numéricas";
            case ParameterSections.BoolLogicOperations: return "Operações lógicas";
            case ParameterSections.IntValues: return "Valores inteiros";
            case ParameterSections.IntOperations: return "Operações matemáticas";
            case ParameterSections.DirectionValues: return "Direções";
            case ParameterSections.MaterialValues: return "Materiais";
            case ParameterSections.ToolValues: return "Ferramentas";
            case ParameterSections.FunctionValues: return "Funções";
            case ParameterSections.SignalValues: return "Sinais";
            case ParameterSections.Robos: return "Robôs";
            case ParameterSections.Construcoes: return "Construções";
            case ParameterSections.Scripts: return "Scripts";
            case ParameterSections.Variaveis: return "Variáveis";
            case ParameterSections.Funcoes: return "Funções";
            case ParameterSections.Signals: return "Sinais";
            case ParameterSections.StringValues: return "Texto";
            case ParameterSections.VariableValues: return "Variáveis";
            case ParameterSections.EntityGeneralValues: return "Tipo Geral";
            case ParameterSections.EntityConstructionValues: return "Tipo de Construção";
            case ParameterSections.EntityResourceValues: return "Tipo de Recurso";
            case ParameterSections.BoolEntityOperations: return "Operações com entidades";
            default: return section.ToString();
        }
    }
    // ???criação de bloco: definicao de tipo por inspector (dicionario onde pode ser adicionado parametros e cada parametro tem uma variavel tipo a ser atribuida)
        // alterar imagem do slot, que nem no scratch (int é quadrado, boolean é losango, etc) [cor/contorno também?]

    public ReferenceHolder refs;
    [SerializeField] private SectionDatabase sectionDatabase;

    [HideInInspector] public GameObject inputTextInstance; 
    
    [HideInInspector] public ParamData paramData;

    public string GetValueAt(ParameterSections section, int index) {  
        var valuesList = sectionDatabase.GetValuesForSection(section);
        if (index < 0 || index >= valuesList.Count) return string.Empty; // ex.: seção ainda não settada no SectionDatabase
        
        return valuesList[index];
    }
    
    public virtual void Setup(string name) // trocar essa função INTEIRA direto pelo InitializeSection()? (melhor nao, ja que essa da setup em varios sections e outras coisas)
    {
        if (paramData == null) {
            Debug.LogError($"[Parametros] Não existe ParamData configurado para o tipo '{refs.type}'. Crie o asset (ex.: ImportTargetParameter / ImportElementParameter) e registre no ParamDatas.");
            return;
        }

        // Seção [entidade]: lista dinâmica montada a partir do catálogo do SectionDatabase.
        if (paramData.type == ParameterType.EntityParameter) {
            refs.dynamicValueProvider = GetEntityValues;
        }

        if (string.IsNullOrEmpty(name)) {
            name = paramData.defaultParamName;
        } 

        if (paramData.CanInputInt) { 
            var inputText = Instantiate(refs.inputTextPrefab, refs.childParamLayout.transform);
            if (int.TryParse(name, out _)) {
                inputText.GetComponent<TMP_InputField>().text = name;
                inputText.SetActive(true);
                name = GetValueAt(ParameterSections.IntValues, 0);
            }
            refs.inputTextInstance = inputText;
        } 

        if (paramData.CanInputText) { // signal/string: input livre pra digitar texto
            var inputText = Instantiate(refs.inputTextPrefab, refs.childParamLayout.transform);
            var inputField = inputText.GetComponent<TMP_InputField>();
            // o prefab base (IntInputField) só aceita int; força aceitar texto livre
            if (inputField != null) {
                inputField.contentType = TMP_InputField.ContentType.Standard;

                // limite de caracteres (string livre): evita balões gigantes
                int limite = paramData.maxCaracteres > 0
                    ? paramData.maxCaracteres
                    : (paramData.type == ParameterType.StringParameter ? LimiteCaracteresPadrao : 0);
                if (limite > 0) inputField.characterLimit = limite;
            }

            // sentinel do tipo ("Digitar..." do signal ou do texto)
            string sentinel = GetValueAt(SectionForType(paramData.type), 0);
            if (!string.IsNullOrEmpty(name) && name != paramData.defaultParamName) {
                // valor já definido (ex.: load): pré-preenche o input e usa o sentinel
                inputField.text = name;
                inputText.SetActive(true);
                name = sentinel;
            }

            var parameterConfig = GetComponent<ParameterConfig>();
            if (paramData.type == ParameterType.SignalParameter) {
                // registra/adiciona o signal digitado na lista UI (e renomeia com propagação, se for o caso)
                inputField.onEndEdit.AddListener(parameterConfig.OnSignalInputEndEdit);
            } else {
                // texto livre: só guarda o texto, sem registrar nada no ExecutionManager
                inputField.onEndEdit.AddListener(parameterConfig.OnStringInputEndEdit);
            }
            refs.inputTextInstance = inputText;
        }

        refs.placeholderLabel.text = name;
        refs.placeholderLabel.ForceMeshUpdate();

        foreach (var section in paramData.sections){
            var sectionOptions = refs.blocksFactory.InitializeSection(section, refs.sectionContent.transform);

            // Seções dinâmicas (bloco de import): a lista é montada em runtime pelo provider
            // do bloco — não vem do SectionDatabase.
            if (IsDynamicSection(section)) continue;

            var values = sectionDatabase.GetValuesForSection(section);
            foreach (var value in values) {
                sectionOptions.valueList.Add(value);
            }
        }

        refs.sectionContent.GetComponent<AdjustWidthByText>().AdjustWidth();// precisa?

        refs.variableManager.OnVariableRenamed += refs.HandleVariableRenamed;
        refs.variableManager.OnVariableRetyped += refs.HandleVariableRetyped;
        refs.executionManager.OnSignalRenamed += refs.HandleSignalRenamed;
        refs.executionManager.OnFunctionRenamed += refs.HandleFunctionRenamed;
    }
}
