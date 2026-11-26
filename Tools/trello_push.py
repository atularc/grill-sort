"""Push the task list in Docs/LevelDesignPlan.md (section 4) to Trello.

In the plan: "### List: <name>" starts a Trello list, "### <title>" is a card, "- <item>" lines under a card
become its checklist.

Usage (PowerShell):
  $env:TRELLO_KEY="..."; $env:TRELLO_TOKEN="..."
  python -I Tools/trello_push.py --dry-run          # show what would be created
  python -I Tools/trello_push.py                    # new board "Grill Sort"
  python -I Tools/trello_push.py --board <boardId>  # add to an existing board (lists are reused by name)

Key: https://trello.com/power-ups/admin -> your Power-Up -> API key. Token: the "Token" link next to the key.
Running it twice creates the cards twice, unless the card already exists in that list (matched by title).
"""
import json, os, sys, urllib.parse, urllib.request

PLAN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Docs", "LevelDesignPlan.md")
API = "https://api.trello.com/1"


def parse(path):
    lists, cur_list, cur_card = [], None, None
    in_tasks = False
    for raw in open(path, encoding="utf-8"):
        line = raw.rstrip()
        if line.startswith("## 4."):
            in_tasks = True
            continue
        if not in_tasks:
            continue
        if line.startswith("## "):
            break
        if line.startswith("### List:"):
            cur_list = {"name": line[len("### List:"):].strip(), "cards": []}
            lists.append(cur_list)
            cur_card = None
        elif line.startswith("### ") and cur_list is not None:
            cur_card = {"name": line[4:].strip(), "items": []}
            cur_list["cards"].append(cur_card)
        elif line.startswith("- ") and cur_card is not None:
            cur_card["items"].append(line[2:].strip())
    return lists


def call(method, path, **params):
    params["key"], params["token"] = os.environ["TRELLO_KEY"], os.environ["TRELLO_TOKEN"]
    url = f"{API}{path}?{urllib.parse.urlencode(params)}"
    req = urllib.request.Request(url, method=method, headers={"Accept": "application/json"})
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8"))


def main():
    args = sys.argv[1:]
    dry = "--dry-run" in args
    board_id = args[args.index("--board") + 1] if "--board" in args else None
    lists = parse(PLAN)
    if dry:
        for l in lists:
            print(f"[{l['name']}]")
            for c in l["cards"]:
                print(f"  - {c['name']}  ({len(c['items'])} checklist items)")
        print(f"{len(lists)} lists, {sum(len(l['cards']) for l in lists)} cards")
        return
    if not os.environ.get("TRELLO_KEY") or not os.environ.get("TRELLO_TOKEN"):
        sys.exit("Set TRELLO_KEY and TRELLO_TOKEN first.")
    if board_id is None:
        board = call("POST", "/boards/", name="Grill Sort", defaultLists="false")
        board_id = board["id"]
        print("Board:", board["url"])
    existing = {l["name"]: l["id"] for l in call("GET", f"/boards/{board_id}/lists")}
    for l in lists:
        list_id = existing.get(l["name"]) or call("POST", "/lists", name=l["name"], idBoard=board_id, pos="bottom")["id"]
        have = {c["name"] for c in call("GET", f"/lists/{list_id}/cards")}
        for c in l["cards"]:
            if c["name"] in have:
                print("  skip (exists):", c["name"])
                continue
            card = call("POST", "/cards", idList=list_id, name=c["name"], pos="bottom",
                        desc="From Docs/LevelDesignPlan.md")
            if c["items"]:
                cl = call("POST", "/checklists", idCard=card["id"], name="Steps")
                for it in c["items"]:
                    call("POST", f"/checklists/{cl['id']}/checkItems", name=it)
            print("  card:", c["name"])
    print("Done.")


if __name__ == "__main__":
    main()
