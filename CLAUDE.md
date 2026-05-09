@AGENTS.md

## Claude Execution Mode
When receiving a task contract from Codex, ChatGPT, GPT-5.5, or another planning agent, treat the contract as the primary source of truth for execution.

Execution rules:
- Start by reading the files and areas named in the contract.
- Keep changes inside the allowed edit scope.
- Do not do unrelated cleanup, formatting, prefab edits, scene edits, or project setting changes.
- If the contract conflicts with existing code or Unity serialization constraints, stop and report the conflict before making broad changes.
- If the task is risky or ambiguous, first reply with the understood goal, intended file changes, approach, and open questions.
- If the task is clear, implement directly and keep the final report concise.

Required final report:
- Changed files
- What behavior changed
- How it was verified
- Remaining risks or unverified areas
