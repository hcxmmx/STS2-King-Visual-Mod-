shader_type canvas_item;

// 背景图层，在场景里把它作为一个 uniform 塞进来
uniform sampler2D background_tex; 
uniform float ripple_speed = 1.0;
uniform float ripple_count = 10.0;
uniform float ripple_amplitude = 0.003; // 振幅，这个值一定要极小

void fragment() {
    // 极其暴力的核心原理：基于 UV 到中心距离，产生正弦波扭曲
    vec2 background_uv = FRAGCOORD.xy * SCREEN_PIXEL_SIZE; // 获取当前屏幕空间的像素
    
    vec2 center = vec2(0.5, 0.5); // 涟漪的中心
    float distance = distance(background_uv, center);
    
    // 产生动态的涟漪纹
    float dynamic_ripple = sin((distance * ripple_count) - (TIME * ripple_speed));
    
    // 应用极其微小的采样偏移
    background_uv.x += dynamic_ripple * ripple_amplitude;
    background_uv.y += dynamic_ripple * ripple_amplitude * 1.5; // 稍微拉伸一下
    
    COLOR = texture(background_tex, background_uv);
}