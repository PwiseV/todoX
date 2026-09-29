using System.Text.Json;
using System.Text.Json.Serialization;

namespace TodoX.Api.Infrastructure;

public class MillisecondDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotImplementedException();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        throw new NotImplementedException();
}

public class NullableMillisecondDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotImplementedException();

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options) =>
        throw new NotImplementedException();
}
