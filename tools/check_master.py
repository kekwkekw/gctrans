"""Read-only Master diagnosis: configuration, package presence, exact key, observed rewrite."""
import argparse
import configparser
import json
import re
from pathlib import Path


def inspect(package, config, table=None, field=None, jp=None, log=None):
    cfg = configparser.ConfigParser(interpolation=None)
    if not cfg.read(config, encoding="utf-8-sig"):
        return {"status": "CONFIG_PATH_MISSING"}
    enabled = cfg.getboolean("Translation", "Enabled", fallback=True)
    tables = {x.strip() for x in cfg.get("Translation.MasterData", "EnabledTables", fallback="*").split(",") if x.strip()}
    path = package / "master.json"
    report = {"master_path": str(path.resolve()), "translation_enabled": enabled,
              "enabled_tables_setting": sorted(tables)}
    if not path.is_file():
        return dict(report, status="MASTER_PATH_MISSING")
    master = json.loads(path.read_text(encoding="utf-8-sig"))
    observed = set(re.findall(r"Master data translated: (\w+)", log.read_text(encoding="utf-8-sig", errors="replace"))) if log else set()
    rows = []
    for name, fields in sorted(master.items()):
        active = enabled and ("*" in tables or name in tables)
        count = sum(bool(ko) and ko != key for values in fields.values() for key, ko in values.items())
        rows.append({"table": name, "enabled": active, "usable_exact_pairs": count,
                     "native_rewrite": "OBSERVED" if name in observed else "NOT_OBSERVED"})
    report.update(status="PACKAGE_INSPECTED", tables=rows,
                  table_count=len(rows), enabled_count=sum(r["enabled"] for r in rows),
                  disabled_count=sum(not r["enabled"] for r in rows))
    if table is not None:
        if not enabled:
            status = "TRANSLATION_DISABLED"
        elif "*" not in tables and table not in tables:
            status = "TABLE_DISABLED"
        elif table not in master:
            status = "TABLE_TRANSLATION_MISSING"
        elif field is not None and field not in master[table]:
            status = "FIELD_TRANSLATION_MISSING"
        elif jp is not None and jp not in master[table][field]:
            status = "EXACT_JP_MISSING"
        elif jp is not None and (not master[table][field][jp] or master[table][field][jp] == jp):
            status = "EMPTY_OR_IDENTITY_TRANSLATION"
        else:
            status = "TRANSLATION_AVAILABLE"
        report["probe"] = {"table": table, "field": field, "jp": jp, "status": status}
        if status == "TRANSLATION_AVAILABLE" and jp is not None:
            report["probe"]["ko"] = master[table][field][jp]
    return report


if __name__ == "__main__":
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, default=root / "translations/ko")
    parser.add_argument("--config", type=Path, default=root / "candidate/GCMod.cfg")
    parser.add_argument("--table")
    parser.add_argument("--field")
    parser.add_argument("--jp")
    parser.add_argument("--log", type=Path)
    args = parser.parse_args()
    if (args.field is not None and args.table is None) or (args.jp is not None and args.field is None):
        parser.error("--field requires --table; --jp requires --field")
    print(json.dumps(inspect(args.package, args.config, args.table, args.field, args.jp, args.log), ensure_ascii=False, indent=2))
