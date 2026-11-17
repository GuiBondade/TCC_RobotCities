using System.Collections.Generic;
using System;

public class StringUtils
{
    // Devolve o texto original ou, se ele já existir na lista, acrescenta sufixos
    // "(1)", "(2)"... sem limite artificial. Usado por scripts, variáveis e nomes
    // de entidades do mapa.
    public static string EnsureUniqueString(string str, IEnumerable<string> stringList, string defaultString) {
        string formattedString = str != null ? str.Trim() : string.Empty;

        if (string.IsNullOrEmpty(formattedString)) {
            formattedString = string.IsNullOrEmpty(defaultString) ? string.Empty : defaultString.Trim();
        }

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (stringList != null) {
            foreach (string item in stringList) {
                if (item != null) existing.Add(item);
            }
        }

        if (!existing.Contains(formattedString)) return formattedString;

        var finalString = formattedString;
        int copyCounter = 1;
        while (existing.Contains(finalString)) {
            finalString = formattedString + " (" + copyCounter + ")";
            copyCounter++;
        }

        return finalString;
    }
}
