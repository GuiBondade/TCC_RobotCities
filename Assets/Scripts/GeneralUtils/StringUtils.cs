using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class StringUtils
{
    private static int copyCounter; 
    private static string formattedString;
    private static string finalString;


    public static string EnsureUniqueString(string str, IEnumerable<string> stringList, string defaultString) {
        formattedString = str.Trim();
        
        if (string.IsNullOrEmpty(formattedString)) {
            formattedString = defaultString;
        }

        Func<string, bool> exists = stringList switch
        {
            ISet<string> set => set.Contains,
            ICollection<string> col when col is not List<string> && col is not string[] => col.Contains,
            _ => new HashSet<string>(stringList, StringComparer.OrdinalIgnoreCase).Contains
        };
        
        finalString = formattedString;
        copyCounter = 1;

        while (exists(finalString) && copyCounter < 10) {
            finalString = $"{formattedString} ({copyCounter})";
            copyCounter++;
        }

        return finalString;
    }
}
