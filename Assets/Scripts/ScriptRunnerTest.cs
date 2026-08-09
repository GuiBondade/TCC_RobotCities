/* using UnityEngine;
using ScriptRunner;

public class ScriptRunnerTest : MonoBehaviour
{
    private string jsonComplexTest = @"
    {
      ""allNodes"": [
        { ""id"": 100, ""type"": ""Block"", ""functionRef"": ""While"", ""parameterIds"": [200], ""bodyBlockId"": 101, ""nextBlockId"": 103 },
        { ""id"": 101, ""type"": ""Block"", ""functionRef"": ""Log"", ""parameterIds"": [203], ""bodyBlockId"": -1, ""nextBlockId"": 102 },
        { ""id"": 102, ""type"": ""Block"", ""functionRef"": ""SetVar"", ""value"": ""Contador"", ""parameterIds"": [204], ""bodyBlockId"": -1, ""nextBlockId"": -1 },
        { ""id"": 103, ""type"": ""Block"", ""functionRef"": ""Log"", ""parameterIds"": [207], ""bodyBlockId"": -1, ""nextBlockId"": -1 },
        
        { ""id"": 200, ""type"": ""Parameter"", ""value"": "">"", ""operandLeft"": 201, ""operandRight"": 202 },
        { ""id"": 201, ""type"": ""Variable"", ""value"": ""Contador"" },
        { ""id"": 202, ""type"": ""Parameter"", ""value"": ""0"" },
        
        { ""id"": 203, ""type"": ""Variable"", ""value"": ""Contador"" },
        
        { ""id"": 204, ""type"": ""Parameter"", ""value"": ""-"", ""operandLeft"": 205, ""operandRight"": 206 },
        { ""id"": 205, ""type"": ""Variable"", ""value"": ""Contador"" },
        { ""id"": 206, ""type"": ""Parameter"", ""value"": ""1"" },
        
        { ""id"": 207, ""type"": ""Parameter"", ""value"": ""Saiu do loop com sucesso!"" }
      ]
    }";

    async void Start()
    {
        ExecutionContext context = new ExecutionContext();
        context.Actor = this.transform;
        context.SetVariable("Contador", 3f); // Começa em 3, vai caindo até 0 e para!

        Debug.Log("Compilando e iniciando teste interno...");
        
        BlockCompiler compiler = new BlockCompiler();
        ExecutionBlock root = compiler.Compile(jsonComplexTest, 100);

        ScriptExecutor executor = new ScriptExecutor(root, context);
        await executor.RunAsync();
    }
} */