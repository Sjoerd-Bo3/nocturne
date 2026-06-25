import os, re
WORK = os.environ["SCHEMA_WORKDIR"]
md = open(os.path.join(WORK, "README.md"), encoding="utf-8").read()
blocks = re.findall(r'```mermaid\n(.*?)```', md, re.S)
for i, b in enumerate(blocks):
    open(os.path.join(WORK, f"blk_{i}.mmd"), "w", encoding="utf-8").write(b)
print(len(blocks))
