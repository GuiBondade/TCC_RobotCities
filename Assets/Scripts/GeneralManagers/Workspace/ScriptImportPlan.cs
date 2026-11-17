using System.Collections.Generic;
using System.Linq;

// ---------------------------------------------------------------------------
// Plano de import: o que um bloco "Importar" traz de um script de origem.
// O mesmo plano é usado pela UI (pra listar as opções e registrar as funções
// importadas) e pela compilação (pra copiar as funções pro script que importa),
// garantindo que os nomes efetivos sejam sempre os mesmos nos dois lados.
// ---------------------------------------------------------------------------

// Chaves usadas no valor salvo do parâmetro "o que importar".
public static class ImportKeys
{
    public const string All = "*";                 // "Tudo"
    public const string None = "";                // nada (seção indisponível)
    public const string FunctionPrefix = "f:";     // função exportada do script
    public const string InternalVarPrefix = "i:";  // variável interna da entidade
    public const string ScriptVarPrefix = "v:";    // variável do script que a entidade executa
    public const string SignalPrefix = "g:";      // signal (entidade ou script)
}

// Uma função que entra no script que importa.
public class ScriptImportFunction
{
    public string effectiveName;  // nome final no script que importa (pode ter sufixo)
    public string originalName;   // nome no script de origem
    public ScriptFunctionInfo info;
    public bool renamed => effectiveName != originalName;
}

// Uma variável que entra no script que importa (via a entidade).
public class ScriptImportVariable
{
    public string localName;      // nome final no script que importa
    public string varName;        // nome na origem
    public bool isScriptVariable; // true = variável do script que a entidade executa (só leitura)
    public bool renamed => localName != varName;
}

// Um bloco de import lido dos nós salvos (antes de resolver a origem).
public class ScriptImportReference
{
    public int blockId;
    public ParameterSections targetSection;   // Robos/Construcoes/Scripts
    public string targetKey;                  // instance id da entidade ou id do script
    public ParameterSections elementSection;  // Variaveis/Funcoes/Signals
    public string elementKey;                 // "*" ou "f:Andar"/"i:x"/"v:x"/"g:x"
}

// Um bloco de import resolvido: origem + seleção + o que foi trazido.
public class ScriptImportPlan
{
    public ScriptData source;                 // script de origem (null = alvo é entidade)
    public string sourceName;                 // nome exibido da origem
    public ParameterSections targetSection;   // Robos/Construcoes/Scripts
    public string targetKey;                  // instance id da entidade ou id do script
    public ParameterSections elementSection;  // Variaveis/Funcoes/Signals
    public string elementKey;                 // "*" ou "f:Andar"/"i:x"/"v:x"/"g:x"

    public List<ScriptImportFunction> functions = new List<ScriptImportFunction>();
    public List<ScriptImportVariable> variables = new List<ScriptImportVariable>();
    public List<string> signals = new List<string>();

    // Mapa nome original -> nome efetivo (usado pra renomear as chamadas internas
    // das funções copiadas).
    public Dictionary<string, string> FunctionRenames = new Dictionary<string, string>();
}

// Regras de nome: colisão entre o importado e o que já existe no script que importa.
public static class ScriptImportNaming
{
    // Gera um nome único pro item importado. Se o nome base já existe no script,
    // ganha o sufixo do nome da origem ("Andar" -> "Andar_Coletor").
    public static string EffectiveName(string baseName, string sourceName, ISet<string> taken) {
        if (string.IsNullOrEmpty(baseName)) return baseName;
        if (taken == null || !taken.Contains(baseName)) return baseName;

        string suffix = Sanitize(sourceName);
        if (string.IsNullOrEmpty(suffix)) suffix = "import";

        string candidate = baseName + "_" + suffix;
        int counter = 2;
        while (taken.Contains(candidate)) {
            candidate = baseName + "_" + suffix + counter;
            counter++;
        }
        return candidate;
    }

    // Deixa o nome usável como sufixo (sem espaços/acentos, mantendo as letras).
    public static string Sanitize(string name) {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        var normalized = name.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (char c in normalized) {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        }

        var clean = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
        var result = new System.Text.StringBuilder(clean.Length);
        foreach (char c in clean) {
            if (char.IsLetterOrDigit(c)) result.Append(c);
            else if (c == ' ' || c == '_' || c == '-') result.Append('_');
        }
        return result.ToString().Trim('_');
    }
}
