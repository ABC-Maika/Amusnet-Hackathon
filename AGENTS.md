<!-- hacky:start -->
## Team coordination (Hacky)

You are one of several agents working for a team. The team shares tasks, progress and decisions through the Hacky tools.

- At the start of a session, call `get_context`. If it shows an answer, notice or teammate question ready, collect it with `check_answers`.
- Your person may run several chats. If `get_context` lists one of their other chats under `my_other_chats` that is idle or ended and has a focus, handoff or open waits, read it. If it is work you should continue, call `resume_session` and carry on from its notes. If it is `active_now`, leave it alone and don't duplicate its work.
- Before working on a task from the team board, call `claim_task`. If it says a teammate owns the task, leave it alone. Call `complete_task` when you finish one.
- When you finish a chunk of work, call `log_progress`, and at least every 15 minutes while you work. Pass `next` with what you'll do next and `task_id` when you know it. Teammates see these as progress updates.
- Before you stop, or when your context is getting full, call `save_handoff` with what is done, what is half done, and what comes next, so your person's next chat can continue without asking.
- If your task depends on work a teammate owns and only they or their agent can tell you, such as an API shape or a file they're changing, call `ask_teammate` instead of guessing. Use `ask` for decisions that need a human. Collect the reply with `check_answers`.
- If `check_answers`, `get_context` or a `heads_up` shows a teammate's question, answer it with `answer_teammate` from what you know. If you can't, ask your own person, or say you don't know. Don't leave it open.
- If a task depends on a product decision, business rule, or fact that isn't in the request or the code, do not guess it and do not ask your own human. Call `search_decisions` first. If no result decides it, call `ask` before writing the code that depends on it, passing `task_id` or `files` when you know them.
- If `ask` returns `possible_duplicates`, your question wasn't sent. If a candidate asks exactly the same thing, call `join_question`. If it only looks similar, for example a different subject, audience or limit, call `ask` again with `confirmed_new: true`.
- If you can't do useful work without the answer, call `check_answers` with `wait_seconds: 240` straight after `ask` or `join_question`. Your teammates are human and may take several minutes to reply. Waiting uses no tokens.
- If the answer only changes a detail you can easily change later, pass what you'll use as `assumption` in `ask` or `join_question` and keep working. Call `check_answers` without `wait_seconds` after each step.
- Before you finish, if an answer is still pending, call `check_answers` with `wait_seconds: 240`, up to three times. Follow the answer exactly. If it is still pending after that, finish what you can and say clearly that the answer is still pending.
- If `check_answers` returns a notice, a human rejected an assumption or changed a decision, and it overrides your earlier work. Change what you built on it right away and say what you changed. Do the same if an answer differs from `your_assumption`.
- Do not use `ask` for things you can work out from the code, or for ordinary engineering choices.
- Never put secrets in anything you send to these tools.
- If this is a Unity project (it has `Assets/` and `ProjectSettings/`): never edit `Library/`, `Temp/`, `Obj/`, `Logs/` or `UserSettings/`; keep each `.meta` file with its asset when you move, rename or delete it, and never change its `guid`; list the `.unity`, `.prefab` and `.asset` files a task touches in `files` when you create it, because two people's edits to one can't be merged. If `claim_task` returns a warning about `unmergeable_files`, don't edit those files until you have agreed with the owner through `ask_teammate`.
<!-- hacky:end -->
