namespace FbsDumper.Assembly;

public static class EnumMapper
{
    public static string MapGenerateType(string value)
    {
        return value switch
        {
            "0" => "Object",
            "1" => "VersionTolerant",
            "2" => "CircularReference",
            "3" => "Collection",
            "4" => "NoGenerate",
            _ => value
        };
    }

    public static string MapSerializeLayout(string value)
    {
        return value switch
        {
            "0" => "Sequential",
            "1" => "Explicit",
            _ => value
        };
    }

    public static bool IsDefaultGenerateType(string? value)
    {
        return string.IsNullOrEmpty(value) || value == "Object" || value == "0";
    }

    public static bool IsDefaultSerializeLayout(string? value)
    {
        return string.IsNullOrEmpty(value) || value == "Sequential" || value == "0";
    }
}

