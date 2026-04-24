shader_type canvas_item;

// 可以在右侧属性栏随时调节速度
uniform vec2 scroll_speed = vec2(-0.3, -0.5);
// 增加一个缩放倍率，如果觉得雨太稀疏了可以调大它
uniform float count_scale = 1.0;

void fragment() {
    // 这里的 UV * count_scale 可以让你在不改图片的情况下改变雨的密度
    vec2 shifted_uv = (UV * count_scale) + (scroll_speed * TIME);

    // 关键：因为你在导入时开启了 Repeat，这里的 texture 采样会自动无限循环
    COLOR = texture(TEXTURE, shifted_uv);
}