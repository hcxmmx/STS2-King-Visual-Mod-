shader_type canvas_item;

// 这里需要一张无缝的噪声图，在 Godot 里新建一个 NoiseTexture2D 即可
uniform sampler2D noise_tex : repeat_enable, filter_linear_mipmap;
uniform float distortion_intensity = 0.01; // 扭曲强度
uniform float speed_scale = 1.0;          // 跃动速度

void fragment() {
    // 极其暴力的核心原理：采样噪声，根据时间产生动态偏移
    vec2 dynamic_uv = UV;
    float time = TIME * speed_scale;
    
    // 让噪声在 X 和 Y 上产生不同的复合偏移
    float noise_val = texture(noise_tex, dynamic_uv * 2.0 + vec2(time * 0.1, time * -0.2)).r;
    dynamic_uv.x += (noise_val - 0.5) * distortion_intensity;
    dynamic_uv.y += (noise_val - 0.5) * distortion_intensity;
    
    COLOR = texture(TEXTURE, dynamic_uv);
}