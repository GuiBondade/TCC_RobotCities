using System.Collections.Generic;

// ---------------------------------------------------------------------------
// Copia uma subárvore de blocos (e seus parâmetros) de um SaveNodesData para
// outro, remapeando os ids pra não colidir com os do destino.
// Usado pra trazer o corpo de uma função exportada de outro script.
// ---------------------------------------------------------------------------
public class NodeGraphCopier
{
    private readonly SaveNodesData _source;
    private readonly SaveNodesData _target;

    private readonly Dictionary<int, int> _blockMap = new Dictionary<int, int>();
    private readonly Dictionary<int, int> _paramMap = new Dictionary<int, int>();
    private readonly Dictionary<int, ParameterNode> _copiedParams = new Dictionary<int, ParameterNode>();

    private int _nextBlockId;
    private int _nextParamId;

    // Id da raiz da cópia em andamento (o declare copiado): os blocos do corpo
    // copiado apontam pra ele como rootId.
    private int _copyRootId = -1;

    // Renomeia funções chamadas dentro dos blocos copiados (nome original -> nome efetivo).
    private readonly Dictionary<string, string> _functionRenames;

    public NodeGraphCopier(SaveNodesData source, SaveNodesData target, Dictionary<string, string> functionRenames = null) {
        _source = source;
        _target = target;
        _functionRenames = functionRenames;

        _nextBlockId = NextFreeBlockId(target);
        _nextParamId = NextFreeParamId(target);
    }

    // Copia a função declarada no bloco 'declareBlockId' (corpo + parâmetros + variáveis locais).
    // 'newName' sobrescreve o nome da função copiada (usado quando houve colisão de nome).
    // Devolve o id do declare copiado (-1 se não achou o bloco).
    public int CopyFunction(int declareBlockId, string newName = null) {
        if (_source == null || _target == null) return -1;

        var declare = FindBlock(declareBlockId);
        if (declare == null) return -1;

        int newDeclareId = CopyBlock(declareBlockId, false, newName);

        // Variáveis locais: copiadas e ligadas ao novo declare.
        if (_source.functionLocals != null) {
            foreach (var entry in _source.functionLocals) {
                if (entry == null || entry.functionBlockId != declareBlockId) continue;

                var copyEntry = new FunctionLocalVars { functionBlockId = newDeclareId, vars = new List<CreateVarNode>() };
                if (entry.vars != null) {
                    foreach (var v in entry.vars) {
                        if (v == null) continue;
                        copyEntry.vars.Add(new CreateVarNode {
                            varName = v.varName,
                            type = v.type,
                            section = v.section,
                            defaultValue = v.defaultValue,
                            position = v.position,
                            exported = v.exported
                        });
                    }
                }
                _target.functionLocals.Add(copyEntry);
            }
        }

        return newDeclareId;
    }

    // Copia um bloco e sua subárvore. 'copyNext' = false copia o bloco sem a cadeia
    // 'next' (usado no declare: o next do declare pertence à linha da workspace).
    private int CopyBlock(int sourceBlockId, bool copyNext, string newFunctionName = null) {
        if (_blockMap.TryGetValue(sourceBlockId, out var existing)) return existing;

        var src = FindBlock(sourceBlockId);
        if (src == null) return -1;

        var copy = new BlockNode {
            id = _nextBlockId++,
            functionId = src.functionId,
            variableNameReference = src.variableNameReference,
            paramIds = new List<int>(),
            rootId = -1,
            next = -1,
            body = -1,
            elseBlockId = -1,
            exported = src.exported
        };
        _blockMap[sourceBlockId] = copy.id;

        // A raiz da cópia (o declare) é a raiz da própria linha: os blocos do corpo
        // apontam pra ela (igual na workspace original).
        bool isCopyRoot = _copyRootId < 0;
        if (isCopyRoot) _copyRootId = copy.id;

        if (src.paramIds != null) {
            foreach (var paramId in src.paramIds) {
                int copiedParamId = CopyParam(paramId);
                if (copiedParamId >= 0) copy.paramIds.Add(copiedParamId);
            }
        }

        // Nome da função copiada (quando renomeada por colisão).
        if (!string.IsNullOrEmpty(newFunctionName) && copy.paramIds.Count > 0) {
            if (_copiedParams.TryGetValue(copy.paramIds[0], out var nameParam)) {
                nameParam.value = newFunctionName;
                nameParam.varName = null;
            }
        }

        if (src.body >= 0) copy.body = CopyBlock(src.body, true);
        if (src.functionId == "If" && src.elseBlockId >= 0) copy.elseBlockId = CopyBlock(src.elseBlockId, true);
        if (copyNext && src.next >= 0) copy.next = CopyBlock(src.next, true);

        copy.rootId = _copyRootId;

        if (isCopyRoot) _copyRootId = -1;

        _target.blocosData.Add(copy);
        return copy.id;
    }

    private int CopyParam(int sourceParamId) {
        if (_paramMap.TryGetValue(sourceParamId, out var existing)) return existing;

        var src = FindParam(sourceParamId);
        if (src == null) return -1;

        var copy = new ParameterNode {
            id = _nextParamId++,
            type = src.type,
            value = src.value,
            varName = src.varName,
            selectedSection = src.selectedSection,
            leftOperandId = -1,
            rightOperandId = -1
        };
        _paramMap[sourceParamId] = copy.id;
        _copiedParams[copy.id] = copy;
        _target.parametrosData.Add(copy);

        // Chamadas de função dentro do corpo copiado: usam o nome efetivo.
        if (_functionRenames != null && _functionRenames.Count > 0
            && src.type == ParameterType.FunctionParameter && !string.IsNullOrEmpty(src.value)
            && _functionRenames.TryGetValue(src.value, out var newName)) {
            copy.value = newName;
        }

        if (src.leftOperandId >= 0) copy.leftOperandId = CopyParam(src.leftOperandId);
        if (src.rightOperandId >= 0) copy.rightOperandId = CopyParam(src.rightOperandId);

        return copy.id;
    }

    private BlockNode FindBlock(int blockId) {
        if (_source?.blocosData == null) return null;
        foreach (var b in _source.blocosData) {
            if (b != null && b.id == blockId) return b;
        }
        return null;
    }

    private ParameterNode FindParam(int paramId) {
        if (_source?.parametrosData == null) return null;
        foreach (var p in _source.parametrosData) {
            if (p != null && p.id == paramId) return p;
        }
        return null;
    }

    private static int NextFreeBlockId(SaveNodesData target) {
        int max = -1;
        if (target?.blocosData != null) {
            foreach (var b in target.blocosData) {
                if (b != null && b.id > max) max = b.id;
            }
        }
        return max + 1;
    }

    private static int NextFreeParamId(SaveNodesData target) {
        int max = -1;
        if (target?.parametrosData != null) {
            foreach (var p in target.parametrosData) {
                if (p != null && p.id > max) max = p.id;
            }
        }
        return max + 1;
    }
}
