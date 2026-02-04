using System;
using UnityEngine;
using System.Xml.Serialization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using Application = UnityEngine.Application;

[Serializable]
public class FlarePathConfig
{
    public const string CONFIG_FOLDER = "/UserData/FlarePathConfig/";
    private const string DEFAULT_CONFIG_NAME = "Default";
    #region parameter

    //effect's config
    public float fxState;
    public float lengthMultiplier;
    public float trailAlphaMultiplier;
    public float opacityMultiplier ;
    public float wrapOpacityMultiplier ;
    public float wrapFresnelModifier ;
    public float streakProbability  ;
    public float streakThreshold ;
    public float minTemp;
    public float ignitionTemp;
    public float maxTemp;
    
    //particle effect's config


    #endregion
    public static string GetConfigFolderPath(string planetName)
    {
        string folderPath = Application.persistentDataPath + CONFIG_FOLDER+planetName;
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        return folderPath;
    }
    public static string GetConfigPath(string planetName, string configName)
    {
        return Path.Combine(GetConfigFolderPath(planetName), configName + ".xml");
    }
    
    public static List<string> GetAllConfigNames(string planetName)
    {
        if (!Directory.Exists(GetConfigFolderPath(planetName)))
        {
            return new List<string>();
        }

        string[] files = Directory.GetFiles(GetConfigFolderPath(planetName), "*.xml");
        List<string> configNames = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        
        return configNames;
    }
    
    public void SaveToFile(string planetName,string configName)
    {
        try
        {
            string filePath = GetConfigPath(planetName,configName);
            string directory = Path.GetDirectoryName(filePath);
            
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            XmlSerializer serializer = new XmlSerializer(typeof(FlarePathConfig));
            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                serializer.Serialize(stream, this);
            }
            Mod.Log($"FlarePath config '{configName}' saved to: {filePath}");
        }
        catch (System.Exception e)
        {
            Mod.Log($"Failed to save FlarePath Config  '{configName}': {e.Message}");
        }
    }

    
    public static FlarePathConfig LoadFromFile(string planetName,string configName)
    {
        string filePath = GetConfigPath(planetName, configName);
        
        if (!File.Exists(filePath))
        {
            Mod.Log($"FlarePathConfig file '{configName}' not found at {filePath}. Creating default config.");
            FlarePathConfig defaultConfig = CreateDefault();
            defaultConfig.SaveToFile(planetName,configName);
            return defaultConfig;
        }

        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(FlarePathConfig));
            using (FileStream stream = new FileStream(filePath, FileMode.Open))
            {
                FlarePathConfig config = serializer.Deserialize(stream) as FlarePathConfig;
                Mod.Log($"FlarePathConfig  '{configName}' loaded from: {filePath}");
                return config;
            }
        }
        catch (System.Exception e)
        {
            Mod.Log($"Failed to load FlarePathConfig config '{configName}': {e.Message}. Using default config.");
            return CreateDefault();
        }
    }

    public static FlarePathConfig CreateDefault()
    {
        return new FlarePathConfig()
        {
            fxState = 1f,
            lengthMultiplier = 1f,
            trailAlphaMultiplier = 1f,
            opacityMultiplier=1f,
            wrapOpacityMultiplier = 1f,
            wrapFresnelModifier = 1f,
            streakProbability = 1f,
            streakThreshold = 1f,
            minTemp = 1f,
            ignitionTemp = 1f,
            maxTemp = 1f
        };
    }
     public FlarePathConfig Clone()
    {
        return new FlarePathConfig
        {
            fxState=this.fxState,
            lengthMultiplier=this.lengthMultiplier,
            trailAlphaMultiplier=this.trailAlphaMultiplier,
            opacityMultiplier=this.opacityMultiplier,
            wrapOpacityMultiplier=this.wrapOpacityMultiplier,
            wrapFresnelModifier=this.wrapFresnelModifier,
            streakProbability=this.streakProbability,
            streakThreshold=this.streakThreshold,
            minTemp = this.minTemp,
            ignitionTemp = this.ignitionTemp,
            maxTemp = this.maxTemp,
        };
    }

    public FlarePathConfig CopyFrom(FlarePathConfig source)
    {
        return new FlarePathConfig
        {
            fxState = source.fxState,
            lengthMultiplier = source.lengthMultiplier,
            trailAlphaMultiplier = source.trailAlphaMultiplier,
            opacityMultiplier=source.opacityMultiplier,
            wrapOpacityMultiplier = source.wrapOpacityMultiplier,
            wrapFresnelModifier = source.wrapFresnelModifier,
            streakProbability = source.streakProbability,
            streakThreshold = source.streakThreshold,
            minTemp = source.minTemp,
            ignitionTemp = source.ignitionTemp,
            maxTemp = source.maxTemp

        };
    }
}