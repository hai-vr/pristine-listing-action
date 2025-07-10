using System.IO;
using Newtonsoft.Json;

object ourIndex = new
{
    title = "Written by Program.cs",
    generated = DateTime.UtcNow
};

File.WriteAllText("output/index.json", JsonConvert.SerializeObject(ourIndex, Formatting.Indented));
