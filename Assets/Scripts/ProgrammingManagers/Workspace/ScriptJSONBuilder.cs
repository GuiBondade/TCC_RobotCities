using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;


using System;
using System.Linq;

public class ScriptJSONBuilder : MonoBehaviour
{
    private BlockNode[] blocoDadosArray;
    private ParameterNode[] paramDadosArray;

    private int spaceBetweenBlocks = 0;

    public BlockDatas blockDatas;
    public BlocksFactory blocksFactory;
    public RectTransform workspace;

    public void SaveAsJson(SaveNodesData nodes, string name){
        // salvo em: C:\Users\<seu_usuario>\AppData\LocalLow\<CompanyName>[DefaultCompany*]\<ProductName>\workspace.json
        string json = JsonUtility.ToJson(nodes, true); // true = readability (tira quando salvar de verdade)
        var folder = Application.persistentDataPath;
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{name}.json");
        File.WriteAllText(path, json);
    }

    public void BuildBlocksFromData(SaveNodesData data) {
        if (data == null) return;
        
        VariableManager.instancia.Clear(); // FAZER ISSO NA CHAMADA DA CONSTRUÇÃO CASO ESTEJA ABRINDO OUTRO SCRIPT, NORMALMENTE SO ADICIONA
        // E SE ADICIONAR UMA VARIAVEL DE MESMO NOME QUE UMA JA EXISTENTE? TEM QUE VER

        // NA VERDADE, DA DE FAZER QUE QUANDO ADICIONAR UM SCRIPT DIFERENTE EM UM EXISTENTE SER UM BLOCO COMUM, REPRESENTANDO TODO ELE
        // MAS OS BLOCOS TEM INICIO DE LINHA NÉ
        // SEPA QUE: DA DE IMPORTAR OUTROS SCRIPTS COMO MODULOS, DAI SERIA TIPO UM BLOCO DE CRIAR VARIAVEL, MAS DE IMPORTAÇÃO, AI TU PODE
        // CHAMAR O SIGNAL PRA UMA FUNÇÃO DO SCRIPT IMPORTADO (ADICIONA UMA SEÇÃO PRO DROPDOWN DE SIGNALS) E PASSAR OS PARAMETROS
        // AI DARIA PRA VER TAMBÉM DE PODER ADICIONAR AS VARIAVEIS DO SCRIPT IMPORTADO COMO SEÇÃO DO MESMO JEITO
        // SO TERIA QUE FAZER ALGO VISUAL, COMO "nomeImportacao.funcao(ou variavel)" 

        foreach(var varNode in data.variaveisData){
            CreateVariableUI visualVar = blocksFactory.InitializeCreateVariable(workspace, false);
            visualVar.SetupWithData(varNode.varName, varNode.type, varNode.section, varNode.defaultValue);
            visualVar.GetComponent<RectTransform>().localPosition = varNode.position;
        }

        if (data.blocosData == null) return;

        int maxBlockId = data.blocosData.Count > 0 ? data.blocosData.Max(b => b.id) : -1;
        int maxParamId = data.parametrosData.Count > 0 ? data.parametrosData.Max(p => p.id) : -1;

        blocoDadosArray = new BlockNode[maxBlockId + 1];
        paramDadosArray = new ParameterNode[maxParamId + 1];

        foreach (var b in data.blocosData) blocoDadosArray[b.id] = b;
        foreach (var p in data.parametrosData) paramDadosArray[p.id] = p;
        
        BuildRoot(data.blocosData);
    }

    // vai ser algo geral a instanciação de blocos e seus parametros, ate porque vai ser feito com pool
    public void BuildRoot(List<BlockNode> blocosData) { // DA PRA FAZER SER ACESSIVEL PELO PALLETE ITEM E DAI LA SER SO CHAMADA DE FUNÇÃO, AQUI TAMBÉM SERIA
        List<int> rootInstancedList = new List<int> ();
        
        workspace.gameObject.SetActive(false);
        foreach (var bloco in blocosData) {
            int rootBlockId = bloco.rootId; // pegar id bloco instanciado na workspace
            if (rootBlockId >= 0) {
                if (!rootInstancedList.Contains(rootBlockId)) {// ver se esse id é de bloco ja instanciado
                    rootInstancedList.Add(rootBlockId); // se nao, instancia e adiciona na lista
                    spaceBetweenBlocks += 100;
                    InstantiateBlock(rootBlockId, true, workspace); // workspace é ref
                }
                spaceBetweenBlocks = 0;
            } else {
                Debug.Log("rootId não settado");
            }
        }
        workspace.gameObject.SetActive(true);
        StartCoroutine(RebuildDepois());
    }

    private void InstantiateBlock(int blockId, bool isRoot, RectTransform parent) {
        var blockNode = blocoDadosArray[blockId];
        var blockData = blockDatas.GetData(blockNode.functionId);
        
        var blockUI = blocksFactory.InitializeBlockLine(blockData, isRoot, parent);
        // vai ser guardado no BlockNode uma referencia pra position, que vai ser salva na leitura
        // isso só no root, o resto vai ser nulo, e portanto todos vão estar no mesmo lugar que estavam antes
        if (isRoot) blockUI.GetComponent<RectTransform>().localPosition = new Vector2(spaceBetweenBlocks, 0f);

        VariableBlockUI varBlockUI = blockUI is VariableBlockUI casted ? casted : null;
        if (varBlockUI != null && !string.IsNullOrEmpty(blockNode.variableNameReference)) 
        {
            varBlockUI.isLoadingState = true; 
            varBlockUI.varNameDropdown.AtualizarLista(blockNode.variableNameReference);
            
            varBlockUI.isLoadingState = false; 
        }

        foreach (var rootParamId in blockNode.paramIds) { // todos os parametros primordiais
            var paramReference = InstantiateParameter(rootParamId, blockUI.TopSlot); // tem que dar .transform no topslot?   
            blockUI.parameterInitialList.Add(paramReference);
            //layout rebuilder no paramroot?

            if (varBlockUI != null) {
                varBlockUI.valueParameter = paramReference;
            }
        }

        if (blockNode.next >= 0) { // faz sentido usar essa operação?
            var slotNextRect = blockUI.slotNext.GetComponent<RectTransform>();
            InstantiateBlock(blockNode.next, false, slotNextRect);
        }
        if (blockNode.body >= 0) {
            var slotBodyRect = ((BlockUIBody)blockUI).slotBody.GetComponent<RectTransform>();
            InstantiateBlock(blockNode.body, false, slotBodyRect);
        }
    }

    private ReferenceHolder InstantiateParameter(int paramId, RectTransform blockParent) { // DA PRA FAZER SER ACESSIVEL PELO PALLETE ITEM E DAI LA SER SO CHAMADA DE FUNÇÃO
        var paramNode = paramDadosArray[paramId]; 

        string paramText = paramNode.value;
        if (string.IsNullOrEmpty(paramText)) {
            paramText = paramNode.varName;
        }

        var paramReference = blocksFactory.InitializeParameter(paramText, paramNode.type, blockParent);
        var paramRect = paramReference.GetComponent<RectTransform>();
        
        //tem que settar data do param aqui? (dava pra ter tipo um blockData, pra cada, sei la) precisa??? preguiça
        // tirar os operand do refholder, e criar um ParamData meio que pra isso 
        // !!!(nao sei se precisa mesmo, ta de boa por enquanto)
        
        if (paramNode.leftOperandId >= 0) {
            var leftOperand = InstantiateParameter(paramNode.leftOperandId, paramRect);//arrumar algo?
            paramReference.leftOperand = leftOperand;
            leftOperand.transform.SetAsFirstSibling();
        }
        if (paramNode.rightOperandId >= 0) {
            var rightOperand = InstantiateParameter(paramNode.rightOperandId, paramRect);//arrumar algo?
            paramReference.rightOperand = rightOperand;
            rightOperand.transform.SetAsLastSibling();
        }

        return paramReference;
    }

    IEnumerator RebuildDepois()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(workspace);
    }
}
