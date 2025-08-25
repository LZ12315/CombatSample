using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LZ_ModelPostProcess : AssetPostprocessor
{
    //用于在导入新Model和Animation时，自动设置Rotation为Original
    //但目前问题在于这个函数会在更改Animation时自动启用 导致每次都将Rotation设置为Original 无法修改

    //private void OnPostprocessModel(GameObject gameObject)
    //{
    //    var importer = assetImporter as ModelImporter;
    //    if(importer == null) return;
    //    if(importer.animationType != ModelImporterAnimationType.Human) return;  

    //    var clips = importer.clipAnimations;
    //    if(clips == null || clips.Length == 0)
    //        clips = importer.defaultClipAnimations;

    //    foreach (var clip in clips)
    //    {
    //        clip.keepOriginalOrientation = true;
    //    }
    //    importer.clipAnimations = clips;
    //}
}
