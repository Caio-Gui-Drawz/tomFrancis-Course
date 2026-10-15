float4 GetPixelValue(in float2 uv) {
    // Amostra os valores de normal e profundidade na cena
    float3 normal = SHADERGRAPH_SAMPLE_SCENE_NORMAL(uv);
    float depth = SHADERGRAPH_SAMPLE_SCENE_DEPTH(uv);
    return float4(normal, 1); // Retorna um valor válido com normal e alfa definidos
}

void Outline_float(in float2 UV, in float2 PixelSize, in float Width, out float4 OffsetSample, out float4 OriginalSample) {
    // Inicializa as variáveis de saída com valores padrão
    OffsetSample = float4(0, 0, 0, 0);
    OriginalSample = float4(0, 0, 0, 0);

    // Define os deslocamentos em 8 direções
    float2 offsets[8] = {
        float2(-Width, -Width),
        float2(-Width, 0),
        float2(-Width, Width),
        float2(0, -Width),
        float2(0, Width),
        float2(Width, -Width),
        float2(Width, 0),
        float2(Width, Width)
    };

    // Inicializa sampledValue com um valor padrão
    float4 sampledValue = float4(0, 0, 0, 0);

    // Soma os valores amostrados nas 8 direções
    for (int j = 0; j < 8; j++) {
        sampledValue += GetPixelValue(UV + offsets[j] * PixelSize);
    }

    // Calcula a média dos valores
    sampledValue /= 8;

    // Atribui valores às variáveis de saída
    OffsetSample = sampledValue;
    OriginalSample = GetPixelValue(UV);
}