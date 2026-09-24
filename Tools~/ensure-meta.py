"""补齐 Unity 元数据，保留现有资源标识。"""
from pathlib import Path
import uuid

root = Path(__file__).resolve().parent.parent
for folder in ("Runtime", "Editor", "Analyzers", "Samples~"):
    base = root / folder
    if not base.exists():
        continue
    for path in [base] + sorted(base.rglob("*")):
        # Samples~ 根目录由 UPM 忽略；仅内部可导入的示例资产需要元数据。
        if path == base and folder.endswith("~"):
            continue
        if path.suffix == ".meta":
            continue
        meta = Path(str(path) + ".meta")
        if meta.exists():
            continue
        text = "fileFormatVersion: 2\nguid: " + uuid.uuid4().hex + "\n"
        if path.is_dir():
            text += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
        elif path.suffix == ".cs":
            text += "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
        elif path.suffix == ".asmdef":
            text += "AssemblyDefinitionImporter:\n  externalObjects: {}\n"
        elif path.suffix == ".dll" and folder == "Analyzers":
            text += """labels:
- RoslynAnalyzer
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
"""
        else:
            text += "DefaultImporter:\n  externalObjects: {}\n"
        meta.write_text(text + "  userData:\n  assetBundleName:\n  assetBundleVariant:\n")
