using System.Text.Json;

namespace Nodes.Builder;

public static class Templates
{
    public static string CreateProjectJson(string languageName, string name) =>
        string.Equals(languageName, "Python", StringComparison.OrdinalIgnoreCase) ? Python(name) : CSharp(name);

    private static string CSharp(string name)
    {
        var files = new Dictionary<string, string>
        {
            [$"{name}.csproj"] = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <AssemblyName>__NAME__</AssemblyName>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="cTrader.Automate" Version="*" />
  </ItemGroup>
</Project>
""".Replace("__NAME__", name, StringComparison.Ordinal),
            [$"{name}.cs"] = """
using cAlgo.API;

namespace __NAME__;

[Robot(AccessRights = AccessRights.None)]
public class __NAME__ : Robot
{
    protected override void OnStart() { }
    protected override void OnTick() { }
    protected override void OnStop() { }
}
""".Replace("__NAME__", name, StringComparison.Ordinal)
        };
        return JsonSerializer.Serialize(files);
    }

    private static string Python(string name)
    {
        var files = new Dictionary<string, string>
        {
            [$"{name}.csproj"] = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>__NAME__</AssemblyName>
    <OutputType>Library</OutputType>
    <Language>Python</Language>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="cTrader.Automate" Version="*" />
  </ItemGroup>
</Project>
""".Replace("__NAME__", name, StringComparison.Ordinal),
            [$"{name}.py"] = """
from cTrader.Automate import *

class Bot(Robot):
    def on_start(self): pass
    def on_tick(self): pass
    def on_stop(self): pass
"""
        };
        return JsonSerializer.Serialize(files);
    }
}
