using System.Text.Json.Serialization;

namespace MawMedia.Models.FaceRecognition;

// what a deletion refers to.  a closed set owned by both sides of the contract,
// unlike media.person_status, whose codes maw-media-ai can extend without a
// deploy here - which is why that one is data and this one is a type.
//
// the member names are pinned rather than left to a naming policy so the wire
// values are the same whichever JsonSerializerOptions does the writing: the
// api's camelCase options at the http boundary, or the snake_case options
// FaceRepository uses to build the jsonb payload.
[JsonConverter(typeof(JsonStringEnumConverter<SyncEntityType>))]
public enum SyncEntityType
{
    [JsonStringEnumMemberName("person")]
    Person,

    [JsonStringEnumMemberName("face")]
    Face
}
