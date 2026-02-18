// Liquid Glass Shader for WPF
// Platform: DirectX 9 / Shader Model 3.0

sampler2D inputSampler : register(s0);
sampler2D normalSampler : register(s1);

// Shader constants
float refractionStrength : register(c0);
float noiseScale         : register(c1);
float mouseX             : register(c2);
float mouseY             : register(c3);
float time               : register(c4);
float specularStrength   : register(c5);
float innerShadowSize    : register(c6);
float density            : register(c7);
float mouseRadius        : register(c8);
float mouseFalloffPower  : register(c9);
float mouseOffsetStrength: register(c10);
float edgeHighlightStrength : register(c11);

float smoothstep_custom(float edge0, float edge1, float x)
{
    float t = saturate((x - edge0) / (edge1 - edge0));
    return t * t * (3.0 - 2.0 * t);
}

float4 main(float2 uv : TEXCOORD0) : COLOR
{
    float2 mouse = float2(mouseX, mouseY);
    float strength = refractionStrength * 0.5;
    float scale = noiseScale;

    float2 offset = float2(0.0, 0.0);

    if (scale > 0.01)
    {
        float2 uv1 = uv * scale;
        float3 normal1 = tex2D(normalSampler, uv1).rgb * 2.0 - 1.0;

        float2 uv2 = uv * (scale * 1.5);
        float3 normal2 = tex2D(normalSampler, uv2).rgb * 2.0 - 1.0;

        float3 normal = normalize(normal1 + normal2 * 0.5);
        offset = normal.xy * strength * 0.08;
    }

    float2 mouseDir = uv - mouse;
    float dist = length(mouseDir);
    float radius = max(0.001, mouseRadius);
    float mouseFalloff = 1.0 - smoothstep_custom(0.0, radius, dist);
    mouseFalloff = pow(saturate(mouseFalloff), max(0.01, mouseFalloffPower));

    float2 mouseOffset = float2(0.0, 0.0);
    if (dist > 0.001)
    {
        mouseOffset = normalize(mouseDir) * mouseFalloff * strength * mouseOffsetStrength;
    }

    offset += mouseOffset;

    float2 uvFinal = clamp(uv + offset, 0.001, 0.999);
    float4 color = tex2D(inputSampler, uvFinal);

    // Density blending
    color.rgb = lerp(color.rgb, color.rgb * 0.6, density * 0.5);

    // Specular highlight
    float specDist = length(uv - mouse);
    float specularBase = 1.0 - saturate(specDist / 0.35);
    float specular = specularBase * specularBase * specularBase * specularStrength;
    color.rgb += specular;

    // Inner shadow
    if (innerShadowSize > 0.001)
    {
        float edgeL = smoothstep_custom(0.0, innerShadowSize, uv.x);
        float edgeR = smoothstep_custom(1.0, 1.0 - innerShadowSize, uv.x);
        float edgeT = smoothstep_custom(0.0, innerShadowSize, uv.y);
        float edgeB = smoothstep_custom(1.0, 1.0 - innerShadowSize, uv.y);
        float innerShadow = edgeL * edgeR * edgeT * edgeB;

        color.rgb *= lerp(0.64, 1.0, innerShadow);
    }

    // Edge highlight
    float edgeHighlightSize = 0.008;
    float edgeDist = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
    float edgeHighlight = 1.0 - smoothstep_custom(0.0, edgeHighlightSize, edgeDist);
    color.rgb += edgeHighlight * edgeHighlightStrength;

    // Preserve source alpha to avoid opaque boxes.
    color.a = tex2D(inputSampler, uvFinal).a;

    return color;
}
