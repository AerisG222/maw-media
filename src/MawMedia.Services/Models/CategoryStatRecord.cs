public record CategoryStatRecord(
    Guid? CategoryId,
    string CategoryName,
    string MediaType,
    long MediaCount,
    decimal FileSize,
    long? Duration
);
