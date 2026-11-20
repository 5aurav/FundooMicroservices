namespace Fundoo.Common.Caching;

public class RedisCacheSettings
{
    public const string SectionName = "Redis";

    public string Configuration { get; set; } = "localhost:6379";
    public string InstanceName { get; set; } = "Fundoo:";
    public bool Enabled { get; set; } = true;
}
