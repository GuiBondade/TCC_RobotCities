using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ScriptRunner
{
    public interface IValueProvider { object GetValue(ExecutionContext context); }

    public class LiteralValue : IValueProvider
    {
        private readonly object _value;
        public LiteralValue(object value) => _value = value;
        public object GetValue(ExecutionContext context) => _value;
    }

    public class VariableValue : IValueProvider
    {
        private readonly string _varName;
        public VariableValue(string varName) => _varName = varName;
        public object GetValue(ExecutionContext context) => context.GetVariable(_varName);
    }

    public class OperationValue : IValueProvider
    {
        private readonly IValueProvider _left;
        private readonly IValueProvider _right;
        private readonly Func<object, object, object> _compiledOp; // Modificado para object (suporta matemática e lógica)

        public OperationValue(IValueProvider left, IValueProvider right, Func<object, object, object> compiledOp)
        {
            _left = left;
            _right = right;
            _compiledOp = compiledOp;
        }

        public object GetValue(ExecutionContext context)
        {
            object leftValue = _left != null ? _left.GetValue(context) : null;
            object rightValue = _right != null ? _right.GetValue(context) : null;
            return _compiledOp(leftValue, rightValue);
        }
    }

    public class ExecutionContext
    {
        public RobotScript robotScript { get; set; }
        public int ticksOcupado = 0;
        public ExecutionBlock current;
        // version que falei no ScriptCompilado no ScriptManager
        private readonly Dictionary<string, object> _vars = new Dictionary<string, object>();
        public void SetVariable(string name, object val) => _vars[name] = val;
        public object GetVariable(string name) => _vars.TryGetValue(name, out var v) ? v : null;
    }

    public abstract class ExecutionBlock
    {
        public ExecutionBlock Next { get; set; }
        public ExecutionBlock Body { get; set; }
        public abstract ExecutionBlock Execute(ExecutionContext context);
    }

    public class IfBlock : ExecutionBlock
    {
        private readonly IValueProvider _condition;
        public ExecutionBlock ElseBody { get; set; }

        public IfBlock(IValueProvider condition) => _condition = condition;

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            bool result = (bool)_condition.GetValue(context);
            return result ? Body : (ElseBody ?? Next);
        }
    }

    public class WhileBlock : ExecutionBlock
    {
        private readonly IValueProvider _condition;

        public WhileBlock(IValueProvider condition) => _condition = condition;

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            bool result = (bool)_condition.GetValue(context);

            if (result && Body != null)
            {
                // REDE DE SEGURANÇA: Espera o próximo frame antes de reavaliar.
                // Isso impede que loops mal estruturados travem a Thread principal da Unity.
                /* await Task.Yield();  */
                // ESPERAR UM FRAME, DA PRA SUBSTITUIR POR COROUTINE
                return Body;
            }
            
            return Next;
        }
    }

    public class WalkBlock : ExecutionBlock
    {
        private readonly IValueProvider _distProvider;
        private readonly IValueProvider _dirProvider;

        public WalkBlock(IValueProvider dist, IValueProvider dir) { _distProvider = dist; _dirProvider = dir; }

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            Debug.Log("context.robotScript: " + context.robotScript);
            Debug.Log("context.robotScript.robotMovement: " + context.robotScript.robotMovement);
            RobotMovement robotMovement = context.robotScript.robotMovement;
            if (robotMovement == null) return Next;

            Vector2Int dir = (Vector2Int)_dirProvider.GetValue(context);
            int dist = (int)_distProvider.GetValue(context); // tem que ser inteiro
            
            robotMovement.MoveTo(dir, dist);

            context.ticksOcupado = dist;

            return Next;
        }
    }

    // Bloco Utilitário adicionado para facilitar o teste de Depuração
    public class LogBlock : ExecutionBlock
    {
        private readonly IValueProvider _messageProvider;
        public LogBlock(IValueProvider messageProvider) => _messageProvider = messageProvider;

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            Debug.Log($"[LOG BLOCO]: {_messageProvider.GetValue(context)}");
            return Next;
        }
    }

    // Bloco Utilitário adicionado para modificar variáveis dentro do fluxo
    public class SetVarBlock : ExecutionBlock
    {
        private readonly string _varName;
        private readonly IValueProvider _valueProvider;

        public SetVarBlock(string varName, IValueProvider valueProvider)
        {
            _varName = varName;
            _valueProvider = valueProvider;
        }

        public override ExecutionBlock Execute(ExecutionContext context)
        {
            context.SetVariable(_varName, _valueProvider.GetValue(context));
            return Next;
        }
    }

    public class BlockCompiler
    {
        private BlockNode[] _blockCache;
        private ParameterNode[] _paramCache;

        public ExecutionBlock Compile(SaveNodesData data, int startBlockId)
        {
            int maxBlockId = data.blocosData.Count > 0 ? data.blocosData.Max(b => b.id) : -1;
            int maxParamId = data.parametrosData.Count > 0 ? data.parametrosData.Max(p => p.id) : -1;

            _blockCache = new BlockNode[maxBlockId + 1];
            _paramCache = new ParameterNode[maxParamId + 1];

            foreach (var b in data.blocosData) _blockCache[b.id] = b;
            foreach (var p in data.parametrosData) _paramCache[p.id] = p;

            if (startBlockId < 0 || startBlockId >= _blockCache.Length || _blockCache[startBlockId] == null)
                throw new Exception($"ID inicial {startBlockId} não foi encontrado nos BlockNodes, de SaveNodesData, de tamanho {_blockCache.Length}.");

            return LinkBlock(_blockCache[startBlockId]);
        }

        private ExecutionBlock LinkBlock(BlockNode blockNode)
        {
            if (blockNode == null) return null;

            ExecutionBlock current = CreateBlockInstance(blockNode);

            if (blockNode.body >= 0 && blockNode.body < _blockCache.Length)
                current.Body = LinkBlock(_blockCache[blockNode.body]);

            ExecutionBlock elseBody = null;
            if (blockNode.functionId == "If" && blockNode.elseBlockId >= 0)
            {
                if (blockNode.elseBlockId >= 0 && blockNode.elseBlockId < _blockCache.Length)
                    elseBody = LinkBlock(_blockCache[blockNode.elseBlockId]);
            }

            if (blockNode.next >= 0 && blockNode.next < _blockCache.Length)
                current.Next = LinkBlock(_blockCache[blockNode.next]);

            // Regras de Costura
            if (current is WhileBlock)
            {
                if (current.Body != null)
                {
                    ExecutionBlock lastInBody = GetLastBlockInChain(current.Body);
                    lastInBody.Next = current; 
                }
            }
            else if (blockNode.functionId == "If")
            {
                var ifBlock = (IfBlock)current;
                if (elseBody != null) ifBlock.ElseBody = elseBody;

                if (current.Body != null && current.Next != null)
                {
                    GetLastBlockInChain(current.Body).Next = current.Next;
                }
                if (elseBody != null && current.Next != null)
                {
                    GetLastBlockInChain(elseBody).Next = current.Next;
                }
            }

            return current;
        }

        private ExecutionBlock GetLastBlockInChain(ExecutionBlock block)
        {
            ExecutionBlock current = block;
            while (current.Next != null && current.Next != block) // Evita loop infinito na busca se já costurado
            {
                current = current.Next;
            }
            return current;
        }

        private ExecutionBlock CreateBlockInstance(BlockNode blockNode)
        {
            return blockNode.functionId switch
            {
                "If" => new IfBlock(ResolveValue(blockNode.paramIds[0])),
                "While" => new WhileBlock(ResolveValue(blockNode.paramIds[0])),
                "Walk" => new WalkBlock(ResolveValue(blockNode.paramIds[0]), ResolveValue(blockNode.paramIds[1])),
                "Log" => new LogBlock(ResolveValue(blockNode.paramIds[0])),
                "SetVar" => new SetVarBlock(blockNode.variableNameReference, ResolveValue(blockNode.paramIds[0])),
                _ => throw new Exception($"Bloco desconhecido: {blockNode.functionId}")
            };
        }

        private IValueProvider ResolveValue(int id)
        {
            if (id < 0 || id >= _paramCache.Length || _paramCache[id] == null) return null;

            var paramNode = _paramCache[id];
            if (paramNode.value == "NÃO")
            {
                if (paramNode.rightOperandId < 0 || paramNode.leftOperandId >= 0)
                {
                    throw new Exception("Estrutura inválida para a operação NÃO.");
                }

                IValueProvider right = ResolveValue(paramNode.rightOperandId);
                return new OperationValue(null, right, (_, value) => !(bool)value);
            }

            if (paramNode.leftOperandId >= 0 && paramNode.rightOperandId >= 0)
            {
                IValueProvider left = ResolveValue(paramNode.leftOperandId);
                IValueProvider right = ResolveValue(paramNode.rightOperandId);

                Func<object, object, object> opDelegate = paramNode.value switch
                {
                    "=="  => (l, r) =>  Equals(l, r),
                    "<="  => (l, r) =>  (int) l <= (int)  r,
                    "<"   => (l, r) =>  (int) l <  (int)  r,
                    ">="  => (l, r) =>  (int) l >= (int)  r,
                    ">"   => (l, r) =>  (int) l >  (int)  r,
                    "+"   => (l, r) =>  (int) l +  (int)  r,
                    "-"   => (l, r) =>  (int) l -  (int)  r,
                    "*"   => (l, r) =>  (int) l *  (int)  r,
                    "/"   => (l, r) =>  (int) l /  (int)  r,//divisão por 0???? (impede aqui ou no digitar? acho melhor aqui na execução)
                    "E"   => (l, r) => (bool) l && (bool) r,
                    "OU"  => (l, r) => (bool) l || (bool) r,
                    _ => throw new Exception($"Operador inválido: {paramNode.value}")
                };

                return new OperationValue(left, right, opDelegate);
            }

            if (!string.IsNullOrEmpty(paramNode.varName)) 
                return new VariableValue(paramNode.varName);

            // LINHA ATUALIZADA: Usando o parser unificado para os literais
            object valorConvertido = ValueParser.ParseRawString(paramNode.value);
            return new LiteralValue(valorConvertido);
        }
    }

    public static class ValueParser
    {
        public static object ParseRawString(string rawValue)
        {
            if (string.IsNullOrEmpty(rawValue)) return null;

            switch (rawValue) {
               case "esquerda": return new Vector2Int(-1, 0);
               case "direita": return new Vector2Int(1, 0);
               case "baixo": return new Vector2Int(0, -1);
               case "cima": return new Vector2Int(0, 1);
               case "verdadeiro": return true;
               case "falso": return false;
               // casos de materiais tem que ver ainda
            }

            // 2. Tenta converter para Float/Numérico
            if (int.TryParse(rawValue, out int numericValue))
            {
                return numericValue;
            }

            // 4. Se não for nenhum dos acima, assume que é uma String pura
            return rawValue;
        }
    }
} 