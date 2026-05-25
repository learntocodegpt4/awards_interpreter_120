# awards_interpreter_120

Baseline documentation for productionizing the DynamicExpresso award
interpreter POC into a governed, versioned, multi-award rule runtime.

## ROS-5 baseline

- [POC validation and Fair Work source ingestion baseline](docs/ros-5-poc-validation-and-source-ingestion-baseline.md)
- [Fair Work source ingestion JSON Schema](schemas/fair-work-source-ingestion.schema.json)

## CI gates

Run the productionization quality gates locally with:

```bash
bash scripts/run-ci.sh
```

The gate covers parser unit tests, parser golden corpus tests, compiler
validation, runtime tests, the MA000120 acceptance suite, and golden fixture
approval checks. See [CI gates](docs/qa/ci-gates.md) for individual commands and
fixture approval rules.
