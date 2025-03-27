using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Editor
{
    public class VoxelTools
    {
        [MenuItem("Tools/add optimize symbol")]
        public static void AddSymbol() => AddSymbol("GEN_OPTIMIZE");
        
        [MenuItem("Tools/remove optimize symbol")]
        public static void RemoveSymbol() => RemoveSymbol("GEN_OPTIMIZE");

        private static void AddSymbol(string symbolToAdd)
        {
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string symbolsStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            if (symbolsStr.Contains(symbolToAdd))
                return;
            
            var symbols = symbolsStr.Split(';').ToList();
            symbols.Add(symbolToAdd);
            symbolsStr = string.Join(";", symbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, symbolsStr);

            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            AssetDatabase.Refresh();
        }

        private static void RemoveSymbol(string symbolToRemove)
        {
            BuildTargetGroup targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            string symbolsStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            if(!symbolsStr.Contains(symbolToRemove))
                return;

            var symbols = symbolsStr.Split(';').ToList();
            symbols.Remove(symbolToRemove);
            symbolsStr = string.Join(";", symbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, symbolsStr);
            AssetDatabase.Refresh();
        }   
    }

}