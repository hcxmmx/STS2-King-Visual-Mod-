import os
import shutil
import re

# ================= 配置区 =================
# 原始素材文件夹（长官拆包出的路径）
SOURCE_DIR = r'D:\Dead Cells\ModTools\FlyingSword_Frames'
# 目标整理文件夹（姐姐帮你新建一个）
TARGET_DIR = r'D:\Dead_FlyingSword_Organized_Assets'
# ==========================================

def sort_dead_cells_frames():
    # 如果目标文件夹不存在，则创建一个
    if not os.path.exists(TARGET_DIR):
        os.makedirs(TARGET_DIR)
        print(f"[姐姐提示] 已为您新建目标文件夹：{TARGET_DIR}")

    print("开始扫描文件，请稍候...")
    
    # 计数器
    count_success = 0
    count_ignored = 0

    # 遍历源文件夹
    for filename in os.listdir(SOURCE_DIR):
        # 1. 自动排雷：跳过法线贴图 (_n.png)
        if filename.lower().endswith('_n.png') or '_n.png' in filename.lower():
            count_ignored += 1
            continue
        
        # 2. 匹配命名规则：{动作名}_{帧数}-=-...
        # 正则表达式说明：匹配 (动作名)_(数字)-=-...
        match = re.match(r"^(.*)_(\d+)-=-.*\.png$", filename)
        
        if match:
            animation_name = match.group(1)  # 提取出的动作名，如 AtkKatanaB2
            
            # 创建对应的动作子文件夹
            action_folder = os.path.join(TARGET_DIR, animation_name)
            if not os.path.exists(action_folder):
                os.makedirs(action_folder)
            
            # 执行复制操作（建议用copy，防止操作失误导致原素材丢失）
            src_path = os.path.join(SOURCE_DIR, filename)
            dst_path = os.path.join(action_folder, filename)
            
            shutil.copy2(src_path, dst_path)
            count_success += 1
        else:
            # 不符合规则的文件，统一放进“其他”文件夹
            others_folder = os.path.join(TARGET_DIR, "Others_Unsorted")
            if not os.path.exists(others_folder):
                os.makedirs(others_folder)
            shutil.copy2(os.path.join(SOURCE_DIR, filename), os.path.join(others_folder, filename))

    print("\n" + "="*30)
    print(f"✨ 整理完成！姐姐一共帮您处理了 {count_success + count_ignored} 个文件")
    print(f"✅ 成功分类归档：{count_success} 个序列帧")
    print(f"🚫 自动拦截并过滤法线图：{count_ignored} 个")
    print(f"📂 成果请看：{TARGET_DIR}")
    print("="*30)

if __name__ == "__main__":
    sort_dead_cells_frames()