namespace backend.Models;

public record StoredDoc(
    string FileName,
    string Text,
    List<string> Chunks,
    List<float[]> Embeddings
);
