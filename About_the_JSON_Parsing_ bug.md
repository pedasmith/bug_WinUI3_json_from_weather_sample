# About the JSON Parsing **trim** bug

## Background

Parsing JSON in a .NET Core WinUI3 Net 10 application is supposed to be simple. But it's actually super duper painful and requires hours of research. Along the way, you'll see the IL2026: member 'System.Text.Json.JsonSerializer.Serialize ... which isn't obvious at all.

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

Let's look at what is required to fix.

I made a duplicate of the WeatherForecasts.cs file call WeatherForecast_Fix1.cs. It starts off exactly the same but the namespace is ```SerializeExtra_Fix1``` instead of ```SerializeExtra```. 

### Phase 1: Make a ```JsonSerializerContext```!

The build warning message provides a hint for fixing this. It says to use the ```JsonSerializerContext``` to solve our problems. And luckily, there's some documentation [How to use source generawtion in System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) that shows up as the first result in Bing for "c# jsonserializercontext". So that's what I'll do.


#### Make SourceGenerationContext

The docs say to do this. So I did, in the Fix1 app
```csharp
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(WeatherForecast))]
    internal partial class SourceGenerationContext : JsonSerializerContext { }
```

This instantly fails with a compile-time error:

```
1>C:\Users\USER\source\repos\USER\bug_WinUI3_json_from_weather_sample\WeatherForecast.cs(44,33,44,57): warning IL2026: Using member 'System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.
1>C:\Users\USER\source\repos\USER\bug_WinUI3_json_from_weather_sample\WeatherForecast_Fix1.cs(45,33,45,57): warning IL2026: Using member 'System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. JSON serialization and deserialization might require types that cannot be statically analyzed. Use the overload that takes a JsonTypeInfo or JsonSerializerContext, or make sure all of the required types are preserved.

```

Unfortunately all of these errors are completely bogus. This isn't the problem at all. Per a [StackOverFlow](https://stackoverflow.com/questions/70825664/how-to-implement-system-text-json-source-generator-with-a-generic-class) comment, the actual fix is to add a "TypeInfoPropertyName" to the JsonSerializable attrbibute.

There isn't any useful guidance at [learn.microsoft.com](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonserializableattribute.typeinfopropertyname?view=net-10.0#system-text-json-serialization-jsonserializableattribute-typeinfopropertyname) for why this is needed at all or what the best name is, any restrictions on the name, or what the side-effects are.

Now the code updates look like this:
```csharp
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(WeatherForecast), TypeInfoPropertyName = "WeatherForecastWithPropertyName")]
    internal partial class SourceGenerationContext : JsonSerializerContext { }
```

### Phase 2: use the SourceGenerationContext

Next we have to actually use the SourceGenerationContext. Looking at the [JsonSerializer.Serialize](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializer.serialize?view=net-10.0) documentation, there are **15** overloads. Of these, only three take in a **JsonSerializerContext**. Irritatingly, none of the templated versions take in a JsonSerializerContext, so we have to use the non-templated version **Serialize(Object, Type, JsonSerializerContext)**.

|Name|	Description|
|-----|-----|
|**Serialize(Utf8JsonWriter, Object, Type, JsonSerializerContext)**	|Writes one JSON value (including objects or arrays) to the provided writer.
|Serialize(Utf8JsonWriter, Object, Type, JsonSerializerOptions)	|Writes the JSON representation of the specified type to the provided writer.
|Serialize(Stream, Object, Type, JsonSerializerOptions)	|Converts the provided value to UTF-8 encoded JSON text and write it to the Stream.
|Serialize(Utf8JsonWriter, Object, JsonTypeInfo)	|Writes one JSON value (including objects or arrays) to the provided writer.
|**Serialize(Stream, Object, Type, JsonSerializerContext)**	|Converts the provided value to UTF-8 encoded JSON text and write it to the Stream.
|Serialize(Object, Type, JsonSerializerOptions)	|Converts the value of a specified type into a JSON string.
|Serialize(Stream, Object, JsonTypeInfo)	|Converts the provided value to UTF-8 encoded JSON text and write it to the Stream.
|Serialize(Object, JsonTypeInfo)	|Converts the provided value into a String.
|**Serialize(Object, Type, JsonSerializerContext)**	|Converts the provided value into a String.
|Serialize<TValue>(TValue, JsonSerializerOptions)	|Converts the value of a type specified by a generic type parameter into a JSON string.
|Serialize<TValue>(TValue, JsonTypeInfo<TValue>)	|Converts the provided value into a String.
|Serialize<TValue>(Stream, TValue, JsonSerializerOptions)	|Converts the provided value to UTF-8 encoded JSON text and write it to the Stream.
|Serialize<TValue>(Stream, TValue, JsonTypeInfo<TValue>)	|Converts the provided value to UTF-8 encoded JSON text and write it to the Stream.
|Serialize<TValue>(Utf8JsonWriter, TValue, JsonSerializerOptions)	|Writes the JSON representation of a type specified by a generic type parameter to the provided writer.
|Serialize<TValue>(Utf8JsonWriter, TValue, JsonTypeInfo<TValue>)	|Writes one JSON value (including objects or arrays) to the provided writer.

Now the code looks like this:

```csharp
    var context = new SourceGenerationContext(); // Ideally is a singleton.
    string jsonString = JsonSerializer.Serialize(weatherForecast, typeof(WeatherForecast), context);

```

### Phase 3: Add some options

The code in [learn.microsoft.com](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) explicitly has an indentation option

```csharp
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(WeatherForecast))]
    internal partial class SourceGenerationContext : JsonSerializerContext { }
```

But take a look at the output. It's not indented at all! So we have to add some options to context object we made.

```csharp
    var options = new JsonSerializerOptions { WriteIndented = true };
    var context = new SourceGenerationContext(options); // Ideally is a singleton.
    string jsonString = JsonSerializer.Serialize(weatherForecast, typeof(WeatherForecast), context);
```

And also update the SourceGenerationContext to remove the pointless JsonSourceGenerationOptions
```csharp
    [JsonSerializable(typeof(WeatherForecast), TypeInfoPropertyName = "WeatherForecastWithPropertyName")]
    internal partial class SourceGenerationContext : JsonSerializerContext { }
```

*Later snarky comment:* actually, this can be made to work; see the Phase 4 section (next) to see how! This one weird undocumented trick is all it takes!

And shazam, serialization works. What about deserialization? Let's add that in as well in Phase 4.

### Phase 4: More about JsonSourceGeneration and add deserialization

Let's make deserialization work as well. First step is to refactor the code a little. There will be a static weatherForecast object and a new Serialize() method

But first, let's look again at the ```[JsonSourceGenerationOptions(WriteIndented = true)]``` line that didn't work. It turns out, in a delightfully undocumented way, that the key is how you build your Context object. 

If you **new SourceGenerationContext()** then the options in the attribute are not used. But, you can instead not bother ever making the object yourself! The class includes a "Default" field of the right type and which is a singleton, and that does include the option!

So the code is rewritten:
```csharp
    string jsonString = JsonSerializer.Serialize(weatherForecast, typeof(WeatherForecast), SourceGenerationContext.Default);
```

note that we don't have to have our own copy of the Context at all; we just use the Default one. Of course, this also means putting the JsonSourceGenerationOptions back on the class.

```csharp
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(WeatherForecast), TypeInfoPropertyName = "WeatherForecastWithPropertyName")]
    internal partial class SourceGenerationContext : JsonSerializerContext { }
```

Deserialization will be discussed phase 5 now.

### Phase 5: Deserialization

Deserialization works similar to serialization. Once again you can't use the templated Deserialize(), and of course you have to do a little cast (I like the ```as WeatherForecast``` better than the ```(WeatherForecast)``` cast, but that's just me). 
```csharp
    var deserializedWeatherForecast = JsonSerializer.Deserialize(jsonString, typeof(WeatherForecast), SourceGenerationContext.Default) as WeatherForecast;
```

I also updated the method to do a second serialize, glue together both of the generated JSON strings, and update the XAML to make the TextBlock output be scrollable (I put it into a ScrollViewer) so that we can see the full output. I also undid the earlier refactoring; I thought I was going nto to need it, but I don't.

Nothing snarky here except that it's silly that I can't use the templated versions of Serialize and Deserialize. The point of templated stuff is to make life easier, not harder; it's the code that we instantly jump to in the documentation because it's the code that's newer and more modern.


## Wrap-up

Final action items for developers:

- The JSON parsing in .NET Core WinUI3 Net 10 is super painful and requires hours of research.
- When you make your custom JsonSerializerContext class, include the TypeInfoPropertyName in the JsonSerializable attribute.
- Always use the Default instance of the context instead of making your own instance. This is the one weird trick that makes the indentation options work.
- Never use the templated Serialize() and Deserialize() methods.

No matter what, I do end up with Trim warnings for just dring to use WinUI3 at all.

As a (former) PM an (current) developer: whoever decided that the TRIM facility should be used is a complete fucking idiot. This was hours of work and research just to make simple, obvious code work. The fact that default settings product broken code, but only in Release mode and where the warnings can't actually be cleared up because Microsoft's own code products warnings, is just bogus.

Worse, the documentation for "Json" is just plain wrong now.



```
1>C:\Users\USER\.nuget\packages\microsoft.windows.sdk.net.ref\10.0.19041.57\lib\net8.0\Microsoft.Windows.SDK.NET.dll : warning IL2104: Assembly 'Microsoft.Windows.SDK.NET' produced trim warnings. For more information see https://aka.ms/il2104
1>C:\Users\USER\.nuget\packages\microsoft.windows.sdk.net.ref\10.0.19041.57\lib\net8.0\WinRT.Runtime.dll : warning IL2104: Assembly 'WinRT.Runtime' produced trim warnings. For more information see https://aka.ms/il2104
```

Github main links:

* [**Main bug project link**](https://github.com/pedasmith/bug_WinUI3_json_from_weather_sample)
* [List of commits](https://github.com/pedasmith/bug_WinUI3_json_from_weather_sample/commits/main/)
* [Phase 1 code doesn't work](https://github.com/pedasmith/bug_WinUI3_json_from_weather_sample/commit/da4b9c56c7d85d7a15dad6ff69b0896cfbe4a487#diff-506f7aff729346cc02ff19405b908909244603ef6157307bdbf1f236f64ff828)
* [Phase 2 Fixed](https://github.com/pedasmith/bug_WinUI3_json_from_weather_sample/commit/c7184c05b33f85de6c6e13b25f2be7d177d43f82)
* [Phase 3 Options work](https://github.com/pedasmith/bug_WinUI3_json_from_weather_sample/commit/ca9eef0add7fbe70e0765fdc4800b3c9738062d1)
* [Phase 4 Options work better](https://github.com/pedasmith/bug_WinUI3_json_from_weather_sample/commit/bb4ebb0f92a4619182457352445e358282d670a3)
* [Phase 5: Wrap up](https://github.com/pedasmith/bug_WinUI3_json_from_weather_sample/commit/4075649702796fdd982116bbc7b8985f353b1e0d)



## Handy links
[Incorrect C# sample](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to#serialize-to-formatted-json)

[Sunrise Programmer blogpost](https://sunriseprogrammer.blogspot.com/2026/04/il2104-il2026-trim-and-json-with-winui3.html)

[C# Feedback for Microsoft](https://developercommunity.visualstudio.com/t/IL2026:-Json-Serialize-and-Deserialize-r/11070060?port=1025&fsid=af36d7c1-2c3a-485c-8271-2d3a78fbba85)


