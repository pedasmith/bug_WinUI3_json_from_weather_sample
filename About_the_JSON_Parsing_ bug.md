# About the JSON Parsing **trim** bug

## Background

Parsing JSON in a .NET Core WinUI3 Net 10 application is supposed to be simple. But it's actually super duper painful and requires hours of research.

JSON parsing should be as simple as:

```csharp
var weatherForecast = new WeatherForecast() { } ; // and actually filled in
string jsonString = JsonSerializer.Serialize(weatherForecast);
```

However, this doesn't work. At compile time in Release mode, there's a new "Trim" option that is enabled by default. When the above code is compiled, the build generates a remarkably scary message

```
>C:\Users\USER\source\repos\USER\bug_WinUI3_json_from_weather_sample\WeatherForecast.cs(44,33,44,57): warning IL2026: Using member 'System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
```

And the message is right to be scary. At runtime (Release mode), the JSON will throw with this exception. In Debug mode, it works fine. It also work fine when TRIM is disabled (Project --> Properties --> Publish --> Trim --> No). But in Release mode with Trim enabled, it fails with this exception:

```
Could not load file or assembly 'System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'. The system cannot find the file specified.
```

Note that **Trim** is the default; it's what the big brains at Microsoft think is the best option for performance. And therefore any library that Microsoft publishes and documents should be simple and straightforward to use with that default setting. 

## Reproducing

The project was created as follows:

1. Create a new WinUI3 Net Core Blank Packaged application in Visual Studio Insiders 2026. This will default to .NET 8. 
2. Make a new .CS file called WeatherForecast.cs and add all the code from [How-To Serialize Formatted JSON](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to#serialize-to-formatted-json)
    public class Demonstrate_Bug_Program
3. Rename the class ```Program``` to ```Demonstrate_Bug_Program```
4. Rename the method ```Main``` to ```Demonstrate_Bug_Main```
5. Change the method signature to return a string and then return jsonString.


Update the MainWindow.xaml file to have a button and a text output; the button when clicked should call the static ```Demonstrate_Bug_Main``` method and output the result to the textblock. Don't forget to wrap the call in a try catch block!


Note that the code, except for trivial changes, is simply the default WinUI3 program with an exact copy/paste of the JSON sample.

Build and run the program in Release mode, and it crashes!

## Required Fixes

### Phase 1: Make a ```JsonSerializerContext```!

The build warning message provides a hint for fixing this. It says to use the ```JsonSerializerContext``` to solve our problems. And luckily, there's some documentation [How to use source generawtion in System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) that shows up as the first result in Bing for "c# jsonserializercontext". So that's what I'll do.

I made a duplicate of the WeatherForecasts.cs file call WeatherForecast_Fix1.cs. It starts off exactly the same but the namespace is ```SerializeExtra_Fix1``` instead of ```SerializeExtra```. 
#### Make SourceGenreationContext

The docs say to do this. So I did, in the Fix1 app
```csharp
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(WeatherForecast))]
internal partial class SourceGenerationContext : JsonSerializerContext { }
```