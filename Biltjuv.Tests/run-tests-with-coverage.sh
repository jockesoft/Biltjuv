#!/bin/bash
dotnet test --settings coverlet.runsettings
reportgenerator \
  -reports:**/coverage.cobertura.xml \
  -targetdir:coverage-report \
  -reporttypes:Html