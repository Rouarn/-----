from docx import Document
from docx.shared import Cm, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_ALIGN_VERTICAL
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.text import WD_BREAK


FONT = "Microsoft YaHei"
ACCENT = RGBColor(0xC0, 0x45, 0x2C)
INK = RGBColor(0x1D, 0x1B, 0x18)
SUB = RGBColor(0x6B, 0x63, 0x57)
MUTED = RGBColor(0x8C, 0x82, 0x73)
GOLD = RGBColor(0xA6, 0x7C, 0x2E)
GREEN = RGBColor(0x4F, 0x7A, 0x58)
WHITE = RGBColor(0xFF, 0xFF, 0xFF)
LINE = "D8CDBB"
SOFT = "F7F2E8"
ACCENT_SOFT = "F7E4DC"
WHITE_HEX = "FFFFFF"
INK_HEX = "1D1B18"


def set_east_asia(target, name=FONT):
    rpr = target._element.get_or_add_rPr() if hasattr(target, "_element") else target.get_or_add_rPr()
    rfonts = rpr.find(qn("w:rFonts"))
    if rfonts is None:
        rfonts = OxmlElement("w:rFonts")
        rpr.append(rfonts)
    rfonts.set(qn("w:ascii"), name)
    rfonts.set(qn("w:hAnsi"), name)
    rfonts.set(qn("w:eastAsia"), name)


def style_run(run, size=10.5, bold=False, color=INK, italic=False):
    run.font.name = FONT
    set_east_asia(run)
    run.font.size = Pt(size)
    run.font.bold = bold
    run.font.italic = italic
    run.font.color.rgb = color


def shade(cell, hex_color):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), hex_color)
    tc_pr.append(shd)


def cell_margins(cell, top=80, bottom=80, left=100, right=100):
    tc_pr = cell._tc.get_or_add_tcPr()
    tc_mar = tc_pr.first_child_found_in("w:tcMar")
    if tc_mar is None:
        tc_mar = OxmlElement("w:tcMar")
        tc_pr.append(tc_mar)
    for tag, value in (("top", top), ("left", left), ("bottom", bottom), ("right", right)):
        element = tc_mar.find(qn("w:" + tag))
        if element is None:
            element = OxmlElement("w:" + tag)
            tc_mar.append(element)
        element.set(qn("w:w"), str(value))
        element.set(qn("w:type"), "dxa")


def table_borders(table, color=LINE, size=4):
    tbl = table._tbl
    tbl_pr = tbl.tblPr
    borders = tbl_pr.find(qn("w:tblBorders"))
    if borders is None:
        borders = OxmlElement("w:tblBorders")
        tbl_pr.append(borders)
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        element = borders.find(qn("w:" + edge))
        if element is None:
            element = OxmlElement("w:" + edge)
            borders.append(element)
        element.set(qn("w:val"), "single")
        element.set(qn("w:sz"), str(size))
        element.set(qn("w:space"), "0")
        element.set(qn("w:color"), color)


def repeat_header(row):
    tr_pr = row._tr.get_or_add_trPr()
    header = OxmlElement("w:tblHeader")
    header.set(qn("w:val"), "true")
    tr_pr.append(header)


def cant_split(row):
    tr_pr = row._tr.get_or_add_trPr()
    element = OxmlElement("w:cantSplit")
    element.set(qn("w:val"), "true")
    tr_pr.append(element)


def set_widths(table, widths):
    table.autofit = False
    for row in table.rows:
        for index, width in enumerate(widths):
            row.cells[index].width = Cm(width)
    for index, width in enumerate(widths):
        table.columns[index].width = Cm(width)


def add_table(headers, rows, widths, font_size=9.0, first_col_bold=True):
    table = doc.add_table(rows=1, cols=len(headers))
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.style = "Table Grid"
    table_borders(table)
    set_widths(table, widths)
    header = table.rows[0]
    repeat_header(header)
    for i, text in enumerate(headers):
        cell = header.cells[i]
        cell.text = ""
        shade(cell, INK_HEX)
        cell_margins(cell)
        cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
        p = cell.paragraphs[0]
        r = p.add_run(text)
        style_run(r, font_size, True, WHITE)
    for r_index, row_data in enumerate(rows):
        row = table.add_row()
        cant_split(row)
        if r_index % 2 == 1:
            for cell in row.cells:
                shade(cell, "FBF8F2")
        for i, text in enumerate(row_data):
            cell = row.cells[i]
            cell.text = ""
            cell_margins(cell)
            cell.vertical_alignment = WD_ALIGN_VERTICAL.CENTER
            p = cell.paragraphs[0]
            p.paragraph_format.space_after = Pt(0)
            p.paragraph_format.line_spacing = 1.15
            color = INK
            bold = first_col_bold and i == 0
            if i == 0:
                color = INK
            if text in ("P0", "P1", "P2"):
                bold = True
                color = ACCENT if text == "P0" else (GOLD if text == "P1" else MUTED)
            r = p.add_run(str(text))
            style_run(r, font_size, bold, color)
    # Keep short tables on one page where possible.
    for r_index, row in enumerate(table.rows):
        if r_index == len(table.rows) - 1:
            break
        for cell in row.cells:
            for paragraph in cell.paragraphs:
                paragraph.paragraph_format.keep_with_next = True
    return table


def body(text, size=10.5, bold_lead=None, color=INK, space_after=6, italic=False, keep_next=False):
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(space_after)
    p.paragraph_format.line_spacing = 1.45
    if keep_next:
        p.paragraph_format.keep_with_next = True
    if bold_lead:
        r = p.add_run(bold_lead)
        style_run(r, size, True, ACCENT)
    r = p.add_run(text)
    style_run(r, size, False, color, italic)
    return p


def bullet(text, bold_lead=None):
    p = doc.add_paragraph(style="List Bullet")
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.line_spacing = 1.4
    if bold_lead:
        r = p.add_run(bold_lead)
        style_run(r, 10.2, True, INK)
    r = p.add_run(text)
    style_run(r, 10.2, False, SUB)
    return p


def heading(text, level=1, new_page=False):
    p = doc.add_heading(level=level)
    p.paragraph_format.keep_with_next = True
    if new_page:
        p.paragraph_format.page_break_before = True
    r = p.add_run(text)
    style_run(r, 16 if level == 1 else (12.5 if level == 2 else 11), True,
              ACCENT if level == 1 else INK)
    if level == 1:
        p.paragraph_format.space_before = Pt(18)
        p.paragraph_format.space_after = Pt(10)
        ppr = p._p.get_or_add_pPr()
        pbdr = OxmlElement("w:pBdr")
        bottom = OxmlElement("w:bottom")
        bottom.set(qn("w:val"), "single")
        bottom.set(qn("w:sz"), "10")
        bottom.set(qn("w:space"), "4")
        bottom.set(qn("w:color"), "C0452C")
        pbdr.append(bottom)
        ppr.append(pbdr)
    elif level == 2:
        p.paragraph_format.space_before = Pt(12)
        p.paragraph_format.space_after = Pt(5)
    else:
        p.paragraph_format.space_before = Pt(8)
        p.paragraph_format.space_after = Pt(3)
    return p


def callout(title, lines, fill=ACCENT_SOFT, bar=ACCENT):
    table = doc.add_table(rows=1, cols=1)
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = False
    table.columns[0].width = Cm(16.4)
    cell = table.rows[0].cells[0]
    cell.width = Cm(16.4)
    shade(cell, fill)
    cell_margins(cell, 140, 140, 180, 160)
    tc_pr = cell._tc.get_or_add_tcPr()
    borders = OxmlElement("w:tcBorders")
    left = OxmlElement("w:left")
    left.set(qn("w:val"), "single")
    left.set(qn("w:sz"), "24")
    left.set(qn("w:color"), "C0452C")
    borders.append(left)
    tc_pr.append(borders)
    p = cell.paragraphs[0]
    p.paragraph_format.space_after = Pt(4)
    r = p.add_run(title)
    style_run(r, 10.5, True, ACCENT)
    for line in lines:
        p = cell.add_paragraph()
        p.paragraph_format.space_after = Pt(2)
        p.paragraph_format.line_spacing = 1.4
        r = p.add_run(line)
        style_run(r, 9.8, False, INK)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)


def add_page_field(paragraph):
    run = paragraph.add_run()
    fld_begin = OxmlElement("w:fldChar")
    fld_begin.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = " PAGE "
    fld_sep = OxmlElement("w:fldChar")
    fld_sep.set(qn("w:fldCharType"), "separate")
    fld_end = OxmlElement("w:fldChar")
    fld_end.set(qn("w:fldCharType"), "end")
    run._r.append(fld_begin)
    run._r.append(instr)
    run._r.append(fld_sep)
    run._r.append(fld_end)
    style_run(run, 8.5, False, MUTED)


def bottom_rule(paragraph, color="D8CDBB", size=4):
    ppr = paragraph._p.get_or_add_pPr()
    pbdr = OxmlElement("w:pBdr")
    bottom = OxmlElement("w:bottom")
    bottom.set(qn("w:val"), "single")
    bottom.set(qn("w:sz"), str(size))
    bottom.set(qn("w:space"), "4")
    bottom.set(qn("w:color"), color)
    pbdr.append(bottom)
    ppr.append(pbdr)


# ==================== 文档与样式 ====================
doc = Document()
section = doc.sections[0]
section.page_width = Cm(21)
section.page_height = Cm(29.7)
section.top_margin = Cm(2.2)
section.bottom_margin = Cm(2.0)
section.left_margin = Cm(2.2)
section.right_margin = Cm(2.2)
section.header_distance = Cm(0.9)
section.footer_distance = Cm(1.0)
section.different_first_page_header_footer = True

styles = doc.styles
normal = styles["Normal"]
normal.font.name = FONT
set_east_asia(normal)
normal.font.size = Pt(10.5)
normal.font.color.rgb = INK
normal.paragraph_format.line_spacing = 1.45
normal.paragraph_format.space_after = Pt(6)

for name, size, color, before, after in (
    ("Heading 1", 16, ACCENT, 18, 10),
    ("Heading 2", 12.5, INK, 12, 5),
    ("Heading 3", 11, ACCENT, 8, 3),
):
    style = styles[name]
    style.font.name = FONT
    set_east_asia(style)
    style.font.size = Pt(size)
    style.font.bold = True
    style.font.color.rgb = color
    style.paragraph_format.space_before = Pt(before)
    style.paragraph_format.space_after = Pt(after)
    style.paragraph_format.keep_with_next = True

for list_name in ("List Bullet", "List Number"):
    style = styles[list_name]
    style.font.name = FONT
    set_east_asia(style)
    style.font.size = Pt(10.2)
    style.font.color.rgb = SUB

# 页眉页脚
header = section.header
hp = header.paragraphs[0]
hp.text = ""
hp.paragraph_format.tab_stops.add_tab_stop(Cm(16.4), alignment=2)
r = hp.add_run("阴阳师工具 · 功能扩展建议")
style_run(r, 8.5, False, MUTED)
r = hp.add_run("\t内部讨论稿 · 2026.10")
style_run(r, 8.5, False, MUTED)
bottom_rule(hp, "E3D8C7", 4)

footer = section.footer
fp = footer.paragraphs[0]
fp.text = ""
fp.paragraph_format.tab_stops.add_tab_stop(Cm(16.4), alignment=2)
r = fp.add_run("OnmyojiTools · v3.0 基线")
style_run(r, 8.5, False, MUTED)
r = fp.add_run("\t第 ")
style_run(r, 8.5, False, MUTED)
add_page_field(fp)
r = fp.add_run(" 页")
style_run(r, 8.5, False, MUTED)

# 文档属性
doc.core_properties.title = "阴阳师工具 · 功能扩展建议"
doc.core_properties.author = "OnmyojiTools"
doc.core_properties.subject = "多开工具功能规划与优先级建议"
doc.core_properties.comments = "基于当前 v3.0 功能基线整理；内部讨论稿。"

# ==================== 封面 ====================
p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(64)
r = p.add_run("ONMYOJI TOOLS  ·  PRODUCT NOTES")
style_run(r, 8.5, True, MUTED)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(16)
p.paragraph_format.space_after = Pt(0)
r = p.add_run("功能扩展建议")
style_run(r, 34, True, INK)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(8)
p.paragraph_format.space_after = Pt(4)
r = p.add_run("从「能多开」到「好管理」的功能地图")
style_run(r, 16, False, ACCENT)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(14)
bottom_rule(p, "C0452C", 18)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(18)
p.paragraph_format.space_after = Pt(28)
r = p.add_run("围绕启动、排列、切换、声音与关闭五个日常动作，梳理值得加入的能力，并给出优先级、技术落点与风险边界。")
style_run(r, 11.5, False, SUB)

meta = doc.add_table(rows=4, cols=2)
meta.autofit = False
meta.columns[0].width = Cm(3.2)
meta.columns[1].width = Cm(10.5)
for row in meta.rows:
    row.cells[0].width = Cm(3.2)
    row.cells[1].width = Cm(10.5)
meta_data = (
    ("文档版本", "v1.0"),
    ("适用基线", "阴阳师工具 v3.0（单屏控制台）"),
    ("编写日期", "2026-10-20"),
    ("文档性质", "内部讨论稿 · 功能规划参考"),
)
for i, (key, value) in enumerate(meta_data):
    c0, c1 = meta.rows[i].cells
    c0.text = ""
    c1.text = ""
    cell_margins(c0, 50, 50, 0, 80)
    cell_margins(c1, 50, 50, 0, 0)
    p0 = c0.paragraphs[0]
    p0.paragraph_format.space_after = Pt(0)
    r0 = p0.add_run(key)
    style_run(r0, 9.2, False, MUTED)
    p1 = c1.paragraphs[0]
    p1.paragraph_format.space_after = Pt(0)
    r1 = p1.add_run(value)
    style_run(r1, 9.8, True, INK)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(36)
r = p.add_run("说明：本文档不涉及游戏内自动化建议；所有功能讨论均以不修改游戏文件、不读取游戏内存、不广播输入为前提。")
style_run(r, 8.8, False, MUTED)

doc.add_page_break()

# ==================== 目录 ====================
p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(2)
p.paragraph_format.space_after = Pt(4)
r = p.add_run("目录")
style_run(r, 22, True, INK)
bottom_rule(p, "C0452C", 12)

toc = doc.add_table(rows=10, cols=2)
toc.autofit = False
toc.columns[0].width = Cm(1.6)
toc.columns[1].width = Cm(14.8)
toc_rows = (
    ("01", "结论先行", "先补工作流，不碰游戏内自动化"),
    ("02", "当前能力基线", "现在已经有什么，边界在哪里"),
    ("03", "用户旅程中的机会", "从启动到关闭，哪些动作仍然费手"),
    ("04", "功能候选全景", "按会话、窗口、声音、监控、系统五类展开"),
    ("05", "推荐路线图", "三个阶段，每个阶段都能独立发布"),
    ("06", "重点功能拆解", "六个最值得优先讨论的功能"),
    ("07", "技术落点与约束", "现有架构怎么接，哪些 API 可用"),
    ("08", "同类工具观察", "别人做了什么，哪些值得借鉴"),
    ("09", "风险与边界", "什么不该做，为什么"),
    ("10", "下一步", "决策清单与下一版建议范围"),
)
for i, (num, title, desc) in enumerate(toc_rows):
    c0, c1 = toc.rows[i].cells
    c0.text = ""
    c1.text = ""
    cell_margins(c0, 80, 80, 0, 60)
    cell_margins(c1, 80, 80, 0, 0)
    p0 = c0.paragraphs[0]
    p0.paragraph_format.space_after = Pt(0)
    r0 = p0.add_run(num)
    style_run(r0, 10, True, GOLD)
    p1 = c1.paragraphs[0]
    p1.paragraph_format.space_after = Pt(0)
    r1 = p1.add_run(title)
    style_run(r1, 11.5, True, INK)
    p2 = c1.add_paragraph()
    p2.paragraph_format.space_after = Pt(0)
    r2 = p2.add_run(desc)
    style_run(r2, 9.2, False, MUTED)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(18)
r = p.add_run("阅读时间约 10 分钟。带 P0 的功能建议优先讨论；估算为开发 + 自测的粗估，不含真实游戏联调时间。")
style_run(r, 9.4, False, MUTED)

doc.add_page_break()

# ==================== 01 结论先行 ====================
heading("01  结论先行")

p = doc.add_paragraph()
p.paragraph_format.space_after = Pt(12)
r = p.add_run("先把「每天都要做」的动作缩短，再考虑更复杂的自动化。")
style_run(r, 14, True, INK)

body("当前工具已经解决了多开、排列、声音跟随和关闭四件事。下一阶段最值得投入的，不是更多游戏内功能，而是让多开后的日常操作更顺手：隐藏/召出、快速切换、单个实例处理、记住布局。")

callout("一句话建议", [
    "优先把「老板键 + 全局快捷键 + 单实例操作菜单」做出来，再补布局预设和每实例音量。",
    "这五项都不修改游戏文件，技术风险低，用户每天都会用到。",
    "游戏内自动战斗、脚本、输入广播、内存读取、自动登录等能力，不纳入本工具规划。",
])

heading("建议的优先级", 2)
add_table(
    ("阶段", "建议内容", "用户价值", "粗估"),
    (
        ("P0 · 下一版", "老板键、全局快捷键、单实例菜单、每实例音量/静音、布局预设", "直接缩短切窗口、调音量、重排窗口的时间", "8–12 人日"),
        ("P1 · 稳定与记忆", "多显示器、健康监控、原始音量恢复、托盘/开机启动、启动就绪检测", "减少“找不到窗口/声音不对/卡死不知道”的焦虑", "12–18 人日"),
        ("P2 · 高级体验", "一键会话、缩略图、实例标签、自动更新、主题", "提升长期使用和展示体验", "18–30 人日"),
        ("不做", "游戏内脚本、输入同步、内存读取、改文件、保存账号密码", "风险高，偏离工具定位", "—"),
    ),
    (2.6, 7.4, 4.1, 2.5),
)

heading("为什么是这五个 P0", 2)
bullet("多开用户最频繁的动作不是“再开一个”，而是在多个窗口之间找窗口、切窗口。", "切窗口：")
bullet("挂机或上班场景需要快速把游戏窗口收起来，老板键比逐个最小化更自然。", "隐藏：")
bullet("每个实例的音量需求不同，且关闭声音跟随后不应该把音量留在 15%。", "声音：")
bullet("每次启动后窗口位置可能变化，布局预设让“排好一次”变成可复用资产。", "布局：")

# ==================== 02 基线 ====================
heading("02  当前能力基线")
body("以下能力以当前 v3.0 代码为准，是后续功能设计的地基，不建议在新增功能时破坏这些约定。", keep_next=True)
add_table(
    ("能力", "当前实现", "边界"),
    (
        ("多开", "按目标开数补足 onmyoji 进程，实例间隔 8 秒", "1–10 开；只认 onmyoji.exe；不自动登录"),
        ("窗口排列", "探测客户端接受尺寸，按 1–4 列网格排布", "主屏工作区；固定 8px 间距；可取消"),
        ("声音跟随", "500ms 检测前台窗口，聚焦 100%、失焦 15%", "关闭时恢复 100%；不还原用户原始百分比"),
        ("一键关闭", "确认后优雅关闭，自动点确认，超时强杀", "只处理标准 #32770 退出框"),
        ("运行实例", "窗口列表、PID、标题、聚焦状态实时更新", "只统计识别到的窗口，不统计无窗口进程"),
        ("刷新", "统一入口在 04 标题右侧，F5 同效", "顶部计数与列表使用同一次窗口快照"),
        ("设置", "游戏目录、目标开数、列数、声音开关", "config.ini 仅当前 key=value；本地不提交"),
        ("诊断", "selftest、假服务 UI 冒烟、crash.log", "真实联调需在可牺牲实例上进行"),
    ),
    (3.0, 8.2, 5.2),
)

heading("需要保持的边界", 2)
bullet("工具不修改任何游戏文件，也不读写游戏内存。", "安全边界：")
bullet("不保存账号密码，不自动登录，不模拟游戏内输入。", "账号边界：")
bullet("所有新增能力都应可关闭、可取消，并且失败时不影响已运行实例。", "体验边界：")
bullet("仍然保持单 exe、无额外运行时；新增依赖优先使用 Windows 自带 API。", "部署边界：")

# ==================== 03 用户旅程 ====================
heading("03  用户旅程中的机会")
body("把一次多开使用拆成七个阶段，能看到“功能很多但动作仍然费手”的位置。", keep_next=True)
add_table(
    ("阶段", "现在怎么做", "仍然存在的问题"),
    (
        ("准备", "打开工具，确认目录、目标开数", "没有配置档案；换账号/换场景要重设"),
        ("启动", "点开始多开，固定等 8 秒/个", "不知道窗口何时真正就绪；失败只能看日志"),
        ("排列", "选列数，点排列", "每次都要重排；多显示器无法指定"),
        ("游玩", "Alt+Tab 找窗口", "窗口多时找不到；没有老板键；没有缩略图"),
        ("切声", "全局焦点跟随，失焦 15%", "不能单实例调音量/静音；不保留原始音量"),
        ("异常", "看日志、手动刷新、重启", "卡死/崩溃没有提醒；不能单实例重启"),
        ("关闭", "确认后全部关闭", "不能只关一个；不能保留部分实例"),
    ),
    (2.2, 7.2, 7.0),
)
body("机会集中在三个词：记住（布局/音量/标签）、切换（老板键/快捷键/缩略图）、可观测（就绪/健康/资源）。")

# ==================== 04 功能候选 ====================
heading("04  功能候选全景")
body("以下为讨论用功能池，不代表都要做。P0 表示建议优先验证，P1 表示第二阶段，P2 表示有空间再做；粗估为开发 + 自测，不含真实游戏联调。", keep_next=True)

heading("4.1  会话与工作区", 2)
add_table(
    ("功能", "解决什么", "粗估", "优先级"),
    (
        ("布局/工作区预设", "保存列数、间距、显示器、窗口槽位，启动后一键套用", "3–4 天", "P0"),
        ("窗口位置/大小记忆", "按实例记住上次位置，减少重复排列", "3–5 天", "P1"),
        ("配置导入/导出", "换机器或备份多套配置", "1–2 天", "P1"),
        ("实例标签/备注", "给窗口起“主号/小号/活动号”等名字和颜色", "3–5 天", "P2"),
        ("一键会话", "启动→等就绪→排列→开启声音，一条流水线", "4–6 天", "P2"),
        ("上次会话恢复", "启动后识别已在运行的实例并套用布局/音量", "3–5 天", "P2"),
    ),
    (3.6, 8.5, 1.7, 1.6),
)

heading("4.2  窗口与快捷操作", 2)
add_table(
    ("功能", "解决什么", "粗估", "优先级"),
    (
        ("老板键", "一键隐藏/恢复全部游戏窗口，适合挂机/办公场景", "2–3 天", "P0"),
        ("全局快捷键切换", "Ctrl+1..9 聚焦、Ctrl+Tab 循环、可自定义", "1–2 天", "P0"),
        ("单实例操作菜单", "右键聚焦、最小化、隐藏、静音、关闭、重启、复制 PID", "2–3 天", "P0"),
        ("多显示器布局", "选择目标显示器，按显示器分别排布", "4–6 天", "P1"),
        ("窗口透明度", "把非焦点窗口降低透明度，减少视觉干扰", "2–3 天", "P2"),
        ("缩略图预览", "在列表/网格中显示窗口实时缩略图，点击聚焦", "5–8 天", "P2"),
        ("拖动/交换槽位", "拖拽窗口交换网格位置，所见即所得", "4–6 天", "P2"),
    ),
    (3.6, 8.5, 1.7, 1.6),
)

heading("4.3  声音与音频", 2)
add_table(
    ("功能", "解决什么", "粗估", "优先级"),
    (
        ("每实例音量/静音", "单独调某个小号，不必受全局 15% 限制", "3–4 天", "P0"),
        ("原始音量记忆/恢复", "关闭跟随后恢复用户原本的百分比，而不是固定 100%", "2–3 天", "P0"),
        ("自定义失焦音量", "15% 可调，或按“静音/低/中”预设", "1–2 天", "P1"),
        ("按标签音量", "主号正常、小号静音等配置随标签保存", "2–3 天", "P2"),
        ("音频设备路由", "把不同实例送到不同输出设备", "高/受限", "P2"),
    ),
    (3.6, 8.5, 1.7, 1.6),
)

heading("4.4  监控与稳定性", 2)
add_table(
    ("功能", "解决什么", "粗估", "优先级"),
    (
        ("启动就绪检测", "等到窗口真正出现/可交互再排列，替代固定 8 秒", "3–4 天", "P1"),
        ("无响应/崩溃检测", "标识卡死窗口，托盘/日志提醒", "3–5 天", "P1"),
        ("资源占用面板", "查看每个实例的 CPU/内存/运行时长", "3–5 天", "P2"),
        ("自动重启实例", "崩溃后按次数/退避重启，不自动登录", "4–6 天", "P2"),
        ("自动排列新窗口", "检测到新窗口后自动放入空槽位", "3–5 天", "P2"),
    ),
    (3.6, 8.5, 1.7, 1.6),
)

heading("4.5  系统集成与体验", 2)
add_table(
    ("功能", "解决什么", "粗估", "优先级"),
    (
        ("托盘/最小化到托盘", "不占任务栏，右键快速切换/显示/退出", "2–3 天", "P1"),
        ("开机自启", "登录 Windows 后自动启动工具", "1 天", "P1"),
        ("日志导出/诊断包", "一键复制环境、窗口、错误信息，方便反馈", "1–2 天", "P1"),
        ("单实例运行", "避免重复启动两个工具抢音量/窗口", "1 天", "P2"),
        ("自动更新", "检查 GitHub Release、下载、替换", "3–5 天", "P2"),
        ("主题/紧凑模式", "深色、紧凑尺寸、不同 DPI 适配", "3–5 天", "P2"),
        ("快捷键自定义", "避免与游戏/系统快捷键冲突", "1–2 天", "P1"),
    ),
    (3.6, 8.5, 1.7, 1.6),
)

callout("优先级判断", [
    "P0 的共同点：不碰游戏内部、失败可恢复、每天都会用。",
    "P1 的共同点：让工具更可靠、更适合长期挂机，但需要更多 Windows 行为验证。",
    "P2 的共同点：体验加分，但成本或不确定性明显更高，适合在 P0/P1 稳定后做。",
], fill="F4EDE1", bar="A67C2E")

# ==================== 05 路线图 ====================
heading("05  推荐路线图")
body("建议每个阶段都能独立发布、独立回滚，不做“大版本一次上齐”。")

heading("阶段一：多开工作流（建议下一版）", 2)
bullet("老板键：隐藏/恢复全部游戏窗口，保留位置，托盘和快捷键都能触发。", "W1  ")
bullet("全局快捷键：聚焦第 N 个实例、循环切换、显示/隐藏全部；可配置。", "W2  ")
bullet("单实例菜单：右键行即可聚焦、最小化、隐藏、静音、关闭、重启。", "W3  ")
bullet("每实例音量：滑杆/静音 + 原始音量记忆，关闭跟随后恢复原值。", "W4  ")
bullet("布局预设：保存/应用列数、间距、显示器、槽位。", "W5  ")
body("预估 8–12 人日。完成后，多开的日常操作会从“找窗口 + 调音量 + 重排”变成几次快捷键和右键。", 9.8, color=SUB)

heading("阶段二：稳定与记忆", 2)
bullet("多显示器布局：选择显示器、按显示器排布、记住 DPI 差异。", "S1  ")
bullet("启动就绪检测：窗口出现并可交互后再排列，减少固定等待。", "S2  ")
bullet("健康监控：无响应/崩溃标识、托盘提醒、运行时长。", "S3  ")
bullet("托盘/开机自启：常驻后台，快速菜单。", "S4  ")
bullet("日志导出/诊断包：反馈问题时一键收集。", "S5  ")

heading("阶段三：高级体验", 2)
bullet("一键会话：启动→就绪→排列→声音，一条可取消的流水线。", "A1  ")
bullet("实例标签/配置档案：主号/小号/活动号，按标签保存布局和音量。", "A2  ")
bullet("缩略图预览：DWM 缩略图或定时截图，点击聚焦。", "A3  ")
bullet("自动更新/主题/紧凑模式。", "A4  ")

callout("现在不建议做", [
    "游戏内自动战斗、自动点击、脚本、输入同步/广播：封号与合规风险最高。",
    "内存读取、修改游戏文件、自动登录/保存密码：安全与维护风险高。",
    "“游戏修复/清理”类功能：涉及文件操作，和当前工具定位不一致。",
])

# ==================== 06 重点功能 ====================
heading("06  重点功能拆解")
body("下面六个功能最值得先讨论。每项都按“场景—交互—技术—风险”四段拆开，方便评估是否进入下一版。")

heading("6.1  老板键 + 全局快捷键", 2)
body("挂机、上班或直播时，需要快速把游戏窗口收起来；多开时又需要快速切到某个实例。", bold_lead="场景：")
body("一个全局热键切换“全部隐藏/全部恢复”；Ctrl+1..9 聚焦第 N 个实例，Ctrl+Tab 循环。快捷键可在设置中修改。", bold_lead="交互：")
body("RegisterHotKey + 一个隐藏消息窗口；ShowWindow(SW_HIDE/SW_SHOW) 隐藏/恢复；隐藏前记录窗口位置，恢复时还原。现有前台检测事件可以复用。", bold_lead="技术：")
body("部分游戏在隐藏后可能暂停渲染或触发重连，需要真实客户端验证；隐藏期间不要自动排列/关闭。快捷键冲突要给出检测和恢复默认。", bold_lead="风险：")
body("2–3 天（热键与隐藏/恢复）；快捷键自定义 +1 天。", bold_lead="粗估：")

heading("6.2  单实例操作菜单", 2)
body("只想关掉一个小号、重启一个卡住的窗口，或者把某个窗口单独调静音，而不是“全部关闭”。", bold_lead="场景：")
body("04 列表右键/行尾菜单：聚焦、最小化、隐藏、静音、关闭、重启、复制 PID/标题。关闭和重启都要确认；重启只重启进程，不自动登录。", bold_lead="交互：")
body("复用 IGameWindowService / IGameProcessService；按 PID+启动时间定位进程，避免 PID 复用误操作。", bold_lead="技术：")
body("重启后需要用户手动登录；关闭单个实例不应影响声音跟随的其他实例。", bold_lead="风险：")
body("2–3 天。", bold_lead="粗估：")

heading("6.3  每实例音量与原始音量", 2)
body("主号需要正常声音，小号希望静音；关闭声音跟随后，不应该把用户原本调好的音量覆盖成 100%。", bold_lead="场景：")
body("列表行内音量滑杆/静音按钮；或在右键菜单里调。声音跟随开启时，手动调整的实例进入“手动覆盖”，不再被 15% 规则覆盖。", bold_lead="交互：")
body("现有 WASAPI 会话服务增加 GetMasterVolume / SetMute / 原始音量缓存；按 PID 记录，进程退出后清理。", bold_lead="技术：")
body("同一进程可能有多个音频会话；设备切换会重建会话；“手动覆盖”与全局跟随的优先级要明确。", bold_lead="风险：")
body("3–4 天（含手动覆盖规则）。", bold_lead="粗估：")

heading("6.4  布局/工作区预设", 2)
body("每天开同样的几个号，希望启动后直接回到熟悉的网格，而不是重新选列数、点排列。", bold_lead="场景：")
body("保存预设名（如“日常 4 开”“活动 8 开”），记录列数、间距、目标显示器、窗口槽位；启动后手动应用或自动应用。", bold_lead="交互：")
body("扩展现有 GridLayout 和设置存储；加入显示器信息（MonitorFromWindow / GetMonitorInfo）；窗口尺寸仍走探测逻辑。", bold_lead="技术：")
body("客户端会锁定尺寸，预设不能假设任意宽高；多显示器/DPI 变化要做回退。", bold_lead="风险：")
body("3–4 天。", bold_lead="粗估：")

heading("6.5  多显示器布局", 2)
body("多屏用户希望主号在副屏、或 8 开分到两块屏，而不是全部挤在主屏。", bold_lead="场景：")
body("布局设置里选择目标显示器；或按窗口数量自动分配到多块屏；每块屏独立列数。", bold_lead="交互：")
body("EnumDisplayMonitors / GetMonitorInfo；用每块屏的工作区替代当前单一 SPI_GETWORKAREA；保持 DPI 感知。", bold_lead="技术：")
body("混合 DPI、任务栏位置、显示器热插拔都会影响坐标；需要边界回退和“重新探测”。", bold_lead="风险：")
body("4–6 天。", bold_lead="粗估：")

heading("6.6  启动就绪检测与一键会话", 2)
body("固定 8 秒/个既慢又不稳：有时窗口好了还在等，有时 8 秒不够。", bold_lead="场景：")
body("启动后轮询窗口是否出现、标题是否可交互；就绪后再排列。进一步可把“启动 N 个→等就绪→排列→开声音”做成一个可取消的会话。", bold_lead="交互：")
body("复用 GameProcessService / GameWindowService，按 PID 等待窗口；带超时、重试和取消；进度写入运行日志。", bold_lead="技术：")
body("登录/加载阶段标题可能变化；不要用过短的超时；取消不能杀掉已经启动的进程。", bold_lead="风险：")
body("3–4 天（就绪检测）；一键会话 +2–3 天。", bold_lead="粗估：")

callout("如果只做三个", [
    "老板键 + 全局快捷键；单实例菜单 + 每实例音量；布局预设。",
    "这三项覆盖“切换、调音、重排”三个最高频动作，且都不涉及游戏内部。",
])

# ==================== 07 技术 ====================
heading("07  技术落点与约束")
body("当前项目是 .NET Framework 4.8 + WPF + 系统自带 csc，无 NuGet、无额外运行时。新增功能优先使用 Windows 自带 API 和现有服务。", keep_next=True)
add_table(
    ("能力", "可用 API / 组件", "备注"),
    (
        ("全局热键", "user32 RegisterHotKey + 隐藏消息窗口", "需要处理热键冲突与注销"),
        ("隐藏/恢复", "ShowWindow、GetWindowRect、SetWindowPos", "隐藏前保存位置；部分游戏可能暂停渲染"),
        ("每实例音量", "现有 WASAPI 会话服务 + GetMasterVolume/SetMute", "扩展服务接口，保持 COM 释放"),
        ("多显示器", "EnumDisplayMonitors、GetMonitorInfo、MonitorFromWindow", "注意混合 DPI"),
        ("缩略图", "dwmapi DwmRegisterThumbnail", "DWM 缩略图开销低；窗口销毁要注销"),
        ("托盘", "System.Windows.Forms.NotifyIcon", "项目已引用 WinForms"),
        ("健康监控", "Process.Responding、SendMessageTimeout(WM_NULL)", "加载期加宽限期，避免误报"),
        ("开机自启", "HKCU 注册表 Run 键", "用户级，不需要管理员"),
        ("自动更新", "HttpClient/WebClient + GitHub Release API", "校验哈希；替换 exe 需退出旧进程"),
        ("配置/档案", "现有 key=value 存储或 JSON", "保持原子写入；不要保存密码"),
    ),
    (3.2, 8.0, 5.2),
)

heading("架构接法", 2)
bullet("新增服务接口（如 IWindowCommandService、IHotkeyService、IProfileStore、IHealthMonitor），由 AppController 编排，UI 只订阅状态。", "服务化：")
bullet("复用 SoundFocusService 的 ForegroundChanged 事件，不要为老板键/快捷键/缩略图再各做一套前台轮询。", "单一来源：")
bullet("所有长任务沿用 CancellationToken + 后台线程；UI 只更新状态，不做阻塞等待。", "异步：")
bullet("每个功能可独立关闭；失败时只影响该功能，不影响多开/排列/关闭的主流程。", "可回滚：")
bullet("selftest 覆盖纯逻辑，UiShot 用假服务覆盖 UI 交互；真实隐藏/透明度/多屏行为在可牺牲实例上联调。", "测试：")

# ==================== 08 同类 ====================
heading("08  同类工具观察")
body("同类工具的能力分布很一致：多开只是入口，真正被长期使用的是“记住窗口状态”和“快速隐藏/切换”。")
body("NGA 的「阳鼠合集」把多开、记住窗口位置/大小/数量/音量/标题、自动音量、老板键、窗口透明度、清理等放在一起，说明用户对“记住 + 一键恢复”有明确需求。", bold_lead="观察一：")
body("开源的 window-summoner 以老板键为核心，支持工作区保存/恢复、自动隐藏其他窗口、托盘/开机自启和多匹配模式；它的功能组合与本工具的“多开窗口管理”高度相似。", bold_lead="观察二：")
body("同类工具里的“游戏修复/清理”涉及文件操作，和本工具“不修改游戏文件”的边界冲突；不建议照搬。", bold_lead="观察三：")

callout("可借鉴 / 不照搬", [
    "值得借鉴：老板键、工作区、记住窗口状态、托盘/快捷键、按窗口匹配。",
    "谨慎评估：窗口透明度（客户端兼容性）、缩略图（性能）、自动重启（循环风险）。",
    "不照搬：文件修复/清理、输入同步、游戏内自动化。",
], fill="F4EDE1", bar="A67C2E")

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(8)
r = p.add_run("参考链接（访问日期 2026-10-20）：")
style_run(r, 9.0, True, INK)
refs = (
    "NGA · 阳鼠合集（多开 + 窗口/音量记忆 + 老板键等）：https://ngabbs.com/read.php?tid=44489806",
    "GitHub · window-summoner（老板键/工作区/托盘）：https://github.com/john-walks-slow/window-summoner",
    "NGA · 桌面版阴阳师多开（窗口位置/大小/数量）：https://ngabbs.com/read.php?tid=42175026",
)
for ref in refs:
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(1)
    p.paragraph_format.line_spacing = 1.2
    r = p.add_run(ref)
    style_run(r, 8.4, False, SUB)

# ==================== 09 风险 ====================
heading("09  风险与边界")
add_table(
    ("风险", "说明", "建议"),
    (
        ("游戏 ToS / 反作弊", "输入自动化、脚本、内存读取、修改文件可能触发封号或反作弊", "不纳入工具；只做外部窗口/音频管理"),
        ("隐藏窗口", "部分客户端隐藏后可能暂停渲染、掉线或无法恢复", "默认关闭；真实客户端验证；保留原位置"),
        ("自动重启", "可能形成重启循环、丢失会话、重复占资源", "默认关闭；最多 N 次 + 退避 + 明确提示"),
        ("全局快捷键", "与游戏/系统/输入法冲突", "可修改、可禁用；冲突检测"),
        ("多显示器/DPI", "混合 DPI、热插拔导致坐标偏移", "边界回退 + 重新探测"),
        ("账号标签", "备注可能包含账号信息", "只存标签/备注，不存密码；本地文件"),
        ("管理员/UIPI", "跨权限窗口操作可能被系统拦截", "保持管理员清单；失败时明确提示"),
        ("维护成本", "功能越多，回归面越大", "分阶段发布；每项可独立关闭"),
    ),
    (3.4, 8.2, 4.8),
)

heading("不建议清单", 2)
for text in (
    "游戏内自动战斗、自动点击、自动任务脚本。",
    "向多个窗口广播键盘/鼠标输入（输入同步）。",
    "读取游戏内存、注入 DLL、修改游戏文件或资源。",
    "保存账号密码、自动登录、绕过登录验证。",
    "游戏修复/清理/加速等涉及文件操作的功能。",
):
    bullet(text)

# ==================== 10 下一步 ====================
heading("10  下一步", new_page=True)
body("建议下一版只做一组“工作流三件套”，先验证高频动作，再决定是否进入多显示器和监控。")
p = doc.add_paragraph()
p.paragraph_format.space_after = Pt(6)
r = p.add_run("建议下一版范围")
style_run(r, 12.5, True, INK)
for idx, text in enumerate((
    "老板键 + 全局快捷键（隐藏/恢复全部，Ctrl+1..9 聚焦，循环切换）。",
    "单实例操作菜单（聚焦/最小化/隐藏/静音/关闭/重启）。",
    "每实例音量 + 原始音量恢复（含手动覆盖规则）。",
    "布局预设（列数/间距/显示器/槽位，手动应用即可）。",
), 1):
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(4)
    p.paragraph_format.left_indent = Cm(0.4)
    r = p.add_run("0" + str(idx) + "  ")
    style_run(r, 10.5, True, GOLD)
    r = p.add_run(text)
    style_run(r, 10.5, False, INK)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(10)
r = p.add_run("决策清单（勾选后即可排期）")
style_run(r, 12.5, True, INK)
for text in (
    "是否需要老板键？隐藏后游戏是否仍保持在线，需要在可牺牲实例上实测。",
    "是否经常使用多显示器？如果是，P1 的多屏布局应提前。",
    "声音是全局跟随就够，还是需要每个实例独立音量/静音？",
    "是否需要给实例起名字/备注？如果不需要，P2 的标签功能可以不做。",
    "是否接受自动重启的风险？如果不确定，先只做“检测 + 提醒”。",
    "是否需要自动更新？如果工具只自己用，P2 可以延后。",
):
    p = doc.add_paragraph()
    p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.left_indent = Cm(0.4)
    r = p.add_run("☐  ")
    style_run(r, 11, False, ACCENT)
    r = p.add_run(text)
    style_run(r, 10.2, False, SUB)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(14)
r = p.add_run("验收标准建议")
style_run(r, 12.5, True, INK)
for text in (
    "隐藏/恢复后，所有窗口回到原来的位置和大小。",
    "快捷键切换后，04 状态列与前台窗口一致。",
    "单实例关闭不影响其他实例，也不影响声音跟随。",
    "关闭声音跟随后，每个实例恢复到自己的原始音量。",
    "布局预设应用失败时，有明确提示且不破坏当前布局。",
):
    bullet(text)

p = doc.add_paragraph()
p.paragraph_format.space_before = Pt(18)
r = p.add_run("一句话：先把“多开之后怎么用”做顺，再考虑“多开能自动做什么”。")
style_run(r, 12, True, ACCENT)

# 保存
import os
os.makedirs(r"D:\Netease\OnmyojiTools\docs", exist_ok=True)
out = r"D:\Netease\OnmyojiTools\docs\功能扩展建议.docx"
doc.save(out)
print(out)
