using System.Collections.Generic;
using SodaCraft.Localizations;
using UnityEngine;

namespace DuckovFontManager
{
    /// <summary>UI strings in Chinese, English, Japanese and Korean.</summary>
    internal static class L10n
    {
        public enum Lang { Zh = 0, En = 1, Ja = 2, Ko = 3 }

        /// <summary>Values accepted by <see cref="FontManagerConfig.panelLanguage"/>.</summary>
        public static readonly string[] Codes = { "auto", "zh", "en", "ja", "ko" };

        public static Lang Current
        {
            get
            {
                switch ((ConfigService.Current.panelLanguage ?? "auto").ToLowerInvariant())
                {
                    case "zh": return Lang.Zh;
                    case "en": return Lang.En;
                    case "ja": return Lang.Ja;
                    case "ko": return Lang.Ko;
                    default: return FromGameLanguage();
                }
            }
        }

        public static Lang FromGameLanguage()
        {
            try
            {
                switch (LocalizationManager.CurrentLanguage)
                {
                    case SystemLanguage.Chinese:
                    case SystemLanguage.ChineseSimplified:
                    case SystemLanguage.ChineseTraditional:
                        return Lang.Zh;
                    case SystemLanguage.Japanese: return Lang.Ja;
                    case SystemLanguage.Korean: return Lang.Ko;
                    default: return Lang.En;
                }
            }
            catch
            {
                return Lang.En;
            }
        }

        public static string T(string key)
        {
            if (!Table.TryGetValue(key, out var row)) return key;
            var s = row[(int)Current];
            return string.IsNullOrEmpty(s) ? row[(int)Lang.En] : s;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        // zh, en, ja, ko
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            ["title"] = new[] { "字体管理器", "Font Manager", "フォントマネージャー", "글꼴 관리자" },
            ["tab"] = new[] { "字体", "Fonts", "フォント", "글꼴" },

            ["fontList"] = new[] { "字体列表（点击即时切换）", "Fonts (click to switch instantly)", "フォント一覧（クリックで即時切替）", "글꼴 목록 (클릭하면 즉시 전환)" },
            ["openFolder"] = new[] { "打开字体目录", "Open font folder", "フォントフォルダーを開く", "글꼴 폴더 열기" },
            ["rescan"] = new[] { "重新扫描", "Rescan", "再スキャン", "다시 검색" },
            ["gameFont"] = new[] { "游戏原版字体", "Game font (original)", "ゲーム標準フォント", "게임 기본 글꼴" },
            ["hdrUser"] = new[] { "自定义字体（ModConfigs/DuckovFontManager/Fonts）", "Custom fonts (ModConfigs/DuckovFontManager/Fonts)", "カスタムフォント（ModConfigs/DuckovFontManager/Fonts）", "사용자 글꼴 (ModConfigs/DuckovFontManager/Fonts)" },
            ["hdrMod"] = new[] { "Mod 内置字体", "Bundled with the mod", "MOD同梱フォント", "모드 내장 글꼴" },
            ["hdrSys"] = new[] { "系统字体", "System fonts", "システムフォント", "시스템 글꼴" },
            ["emptyList"] = new[]
            {
                "还没有自定义字体。\n点击“打开字体目录”，放入 .ttf / .otf 文件即可。",
                "No custom fonts yet.\nClick \"Open font folder\" and drop .ttf / .otf files into it.",
                "カスタムフォントはまだありません。\n「フォントフォルダーを開く」から .ttf / .otf ファイルを入れてください。",
                "사용자 글꼴이 아직 없습니다.\n\"글꼴 폴더 열기\"를 눌러 .ttf / .otf 파일을 넣으세요.",
            },
            ["cantLoad"] = new[] { "（无法加载）", " (cannot load)", "（読み込めません）", " (불러올 수 없음)" },
            ["missingFile"] = new[] { "（文件不存在）", " (file missing)", "（ファイルがありません）", " (파일 없음)" },

            ["preview"] = new[] { "预览", "Preview", "プレビュー", "미리보기" },
            ["previewText"] = new[]
            {
                "逃离鸭科夫 Escape from Duckov\n0123456789  ¥12,345  -128  x3\n【任务】撤离点已开启，快跑！The quick brown fox.",
                "Escape from Duckov - The quick brown fox\n0123456789  $12,345  -128  x3\n[Quest] The extraction point is open. Run!",
                "Escape from Duckov あいうえお アイウエオ\n0123456789  ¥12,345  -128  x3\n【任務】脱出ポイントが開いた。急げ！",
                "Escape from Duckov 가나다라마바사\n0123456789  ₩12,345  -128  x3\n【임무】탈출 지점이 열렸습니다. 서두르세요!",
            },
            ["effective"] = new[] { "当前生效字体：{0}", "Active font: {0}", "使用中のフォント：{0}", "사용 중인 글꼴: {0}" },
            ["disabled"] = new[] { "（替换已关闭）", " (replacement off)", "（置き換え無効）", " (교체 꺼짐)" },
            ["langBound"] = new[] { "当前语言：{0}　已绑定字体：{1}", "Game language: {0} - bound font: {1}", "ゲームの言語：{0}　専用フォント：{1}", "게임 언어: {0} - 지정 글꼴: {1}" },
            ["langUnbound"] = new[]
            {
                "当前语言：{0}　未绑定（使用上面列表中选中的字体）",
                "Game language: {0} - no binding (uses the font selected in the list)",
                "ゲームの言語：{0}　専用フォントなし（一覧で選択したフォントを使用）",
                "게임 언어: {0} - 지정 없음 (목록에서 선택한 글꼴 사용)",
            },
            ["bindLang"] = new[] { "当前语言使用所选字体", "Use selected font for this language", "この言語に選択中のフォントを指定", "현재 언어에 선택한 글꼴 지정" },
            ["clearLang"] = new[] { "清除当前语言绑定", "Clear language binding", "言語ごとの指定を解除", "언어 지정 해제" },

            ["scope"] = new[] { "替换范围", "Replacement scope", "置き換え範囲", "교체 범위" },
            ["enable"] = new[] { "启用字体替换", "Enable font replacement", "フォントの置き換えを有効にする", "글꼴 교체 사용" },
            ["replaceTitle"] = new[] { "替换标题字体（粗体标题）", "Replace the title font (bold headings)", "見出しフォントも置き換える（太字の見出し）", "제목 글꼴도 교체 (굵은 제목)" },
            ["forceArt"] = new[]
            {
                "强制覆盖美术字体（像素、数码管、等宽等风格字体）",
                "Force-replace stylised fonts (pixel, 7-segment, monospace)",
                "デザインフォントも強制的に置き換える（ピクセル・7セグ・等幅など）",
                "디자인 글꼴도 강제로 교체 (픽셀, 7세그먼트, 고정폭 등)",
            },
            ["replaceMods"] = new[] { "替换其他 Mod 自带的字体", "Replace fonts shipped by other mods", "他のMODのフォントも置き換える", "다른 모드의 글꼴도 교체" },
            ["safePanel"] = new[]
            {
                "设置面板保持原版字体（选错字体时也能看清）",
                "Keep this panel in the game font (stays readable)",
                "この画面はゲームのフォントで表示（読めなくなるのを防ぐ）",
                "이 설정 창은 게임 글꼴로 표시 (읽기 어려워지는 것 방지)",
            },

            ["size"] = new[] { "字号", "Font size", "文字サイズ", "글자 크기" },
            ["globalSize"] = new[] { "全局字号", "Overall", "全体", "전체" },
            ["uiSize"] = new[] { "界面文字", "UI text", "UIの文字", "UI 텍스트" },
            ["worldSize"] = new[] { "世界文字（伤害飘字等）", "World text (damage numbers etc.)", "ワールド上の文字（ダメージ表示など）", "월드 텍스트 (피해량 표시 등)" },
            ["titleSize"] = new[] { "标题文字", "Headings", "見出し", "제목" },
            ["normalize"] = new[]
            {
                "视觉字号归一化（让新字体看起来和原字体一样大）",
                "Match visual size (new font looks as large as the original)",
                "見た目のサイズを揃える（元のフォントと同じ大きさに見えるよう調整）",
                "시각적 크기 맞춤 (원래 글꼴과 같은 크기로 보이게 조정)",
            },

            ["layout"] = new[] { "排版微调（仅对已替换的文字生效）", "Layout tweaks (replaced text only)", "レイアウト調整（置き換えた文字のみ）", "배치 미세 조정 (교체된 텍스트만)" },
            ["vOffset"] = new[] { "垂直偏移（正值向上）", "Vertical offset (positive = up)", "縦位置（正の値で上へ）", "세로 위치 (양수 = 위로)" },
            ["lineSpacing"] = new[] { "行距调整", "Line spacing", "行間", "줄 간격" },

            ["advanced"] = new[] { "高级", "Advanced", "詳細設定", "고급" },
            ["prewarm"] = new[] { "预热常用字（减少首次显示卡顿）", "Pre-render common characters (fewer hitches)", "よく使う文字を事前に生成（初回表示のカクつきを軽減）", "자주 쓰는 글자 미리 생성 (첫 표시 끊김 감소)" },
            ["sysFonts"] = new[] { "在列表中显示系统字体", "Show system fonts in the list", "システムフォントを一覧に表示", "시스템 글꼴을 목록에 표시" },
            ["watch"] = new[] { "自动监视字体目录（放入新字体立即可用）", "Watch the font folder (new fonts appear instantly)", "フォントフォルダーを監視（追加したフォントをすぐ使える）", "글꼴 폴더 감시 (새 글꼴 바로 사용 가능)" },
            ["periodic"] = new[] { "定期扫描（排查个别文字未替换时开启）", "Periodic scan (turn on if some text is not replaced)", "定期スキャン（一部の文字が置き換わらない場合にオン）", "주기적 검색 (일부 텍스트가 교체되지 않을 때 켜기)" },
            ["debug"] = new[] { "调试日志", "Debug log", "デバッグログ", "디버그 로그" },
            ["panelLang"] = new[] { "界面语言", "Panel language", "表示言語", "표시 언어" },
            ["auto"] = new[] { "自动", "Auto", "自動", "자동" },
            ["hint"] = new[]
            {
                "快捷键：{0} 打开/关闭本面板。更多选项（排除规则、按字体分类、图集参数）见 config.json。",
                "Hotkey: {0} opens/closes this panel. More options (exclusions, font categories, atlas settings) are in config.json.",
                "ショートカット：{0} でこの画面を開閉。その他の設定（除外ルール、フォント分類、アトラス設定）は config.json にあります。",
                "단축키: {0} 키로 이 창을 열고 닫습니다. 추가 옵션(제외 규칙, 글꼴 분류, 아틀라스 설정)은 config.json에 있습니다.",
            },

            ["reset"] = new[] { "恢复默认", "Reset", "初期設定に戻す", "기본값 복원" },
            ["close"] = new[] { "关闭", "Close", "閉じる", "닫기" },
            ["status"] = new[]
            {
                "已处理文本 {0} · 字体资源 {1} · 补丁 {2}（{3}）",
                "Texts {0} · Font assets {1} · Patches {2} ({3})",
                "テキスト {0} · フォントアセット {1} · パッチ {2}（{3}）",
                "텍스트 {0} · 글꼴 에셋 {1} · 패치 {2} ({3})",
            },
            ["prewarming"] = new[] { " · 预热中 {0}", " · Pre-rendering {0}", " · 事前生成中 {0}", " · 미리 생성 중 {0}" },
            ["scanMode"] = new[] { "扫描模式", "scan mode", "スキャンモード", "검색 모드" },
        };
    }
}
