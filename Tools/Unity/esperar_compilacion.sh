#!/bin/bash
# Pide a Unity que recompile, espera a que termine y muestra los errores y avisos de la consola.
# Necesita el editor abierto con com.unity.pipeline (ver CLAUDE.md §9).
PROJECT="C:\\Users\\devin\\SimuladorArqComp_UNERMB"
unity command recompile --project-path "$PROJECT" --no-banner >/dev/null 2>&1
for i in $(seq 1 60); do
  r=$(unity command recompile_status --project-path "$PROJECT" --no-banner 2>&1)
  echo "$r" | grep -q "completed\|up_to_date" && break
  sleep 3
done
echo "$r" | tail -1
unity command console --level warning --project-path "$PROJECT" --no-banner 2>&1 | grep -o '"message":"[^"]*' | head -20
