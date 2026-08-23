using System.Collections.Generic;
using Vivarium.Domain.Common;

namespace Vivarium.Domain.Decisions
{
    public static class DecisionReasoningProgramValidator
    {
        public static IReadOnlyList<string> Validate(
            DecisionReasoningProgram program,
            IReadOnlyList<DecisionOption> options,
            IReadOnlyCollection<AuthoredId> providerIds)
        {
            var errors = new List<string>();
            if (program == null) return errors;
            var providers = new HashSet<AuthoredId>(providerIds ?? new AuthoredId[0]);
            var bindingIds = new HashSet<AuthoredId>();
            for (int b = 0; b < program.Bindings.Count; b++)
            {
                CompiledConsiderationBinding binding = program.Bindings[b];
                if (!binding.BindingId.IsSet || !bindingIds.Add(binding.BindingId))
                    errors.Add($"reasoning binding id '{binding.BindingId}' is unset or duplicated");
                if (!binding.ConsiderationId.IsSet) errors.Add($"binding '{binding.BindingId}' has no Consideration id");
                if (!binding.ReasonChannel.Id.IsSet) errors.Add($"binding '{binding.BindingId}' has no ReasonChannel id");
                if (!binding.Scale.Id.IsSet || binding.Scale.Thresholds.Count == 0)
                    errors.Add($"binding '{binding.BindingId}' needs a non-empty reason scale");

                var schema = new Dictionary<AuthoredId, ConsiderationParameter>();
                for (int p = 0; p < binding.ParameterSchema.Count; p++)
                {
                    ConsiderationParameter parameter = binding.ParameterSchema[p];
                    if (!parameter.Id.IsSet || schema.ContainsKey(parameter.Id))
                        errors.Add($"binding '{binding.BindingId}' has an unset or duplicate parameter '{parameter.Id}'");
                    else schema.Add(parameter.Id, parameter);
                }
                var bound = new HashSet<AuthoredId>();
                for (int p = 0; p < binding.ParameterBindings.Count; p++)
                {
                    CompiledParameterBinding parameter = binding.ParameterBindings[p];
                    if (!schema.ContainsKey(parameter.ParameterId))
                        errors.Add($"binding '{binding.BindingId}' binds undeclared parameter '{parameter.ParameterId}'");
                    if (!bound.Add(parameter.ParameterId))
                        errors.Add($"binding '{binding.BindingId}' binds parameter '{parameter.ParameterId}' twice");
                    if ((parameter.Source == ParameterBindingSource.DecisionContext ||
                         parameter.Source == ParameterBindingSource.OptionContext) && !parameter.SourceParameterId.IsSet)
                        errors.Add($"binding '{binding.BindingId}' parameter '{parameter.ParameterId}' has no source key");
                    if (schema.TryGetValue(parameter.ParameterId, out ConsiderationParameter declared) &&
                        parameter.Source == ParameterBindingSource.Literal)
                    {
                        if (parameter.Literal.Kind != declared.Kind)
                            errors.Add($"binding '{binding.BindingId}' literal '{parameter.ParameterId}' has type {parameter.Literal.Kind}, expected {declared.Kind}");
                        if (parameter.Literal.Kind == DecisionParameterKind.Entity)
                            errors.Add($"binding '{binding.BindingId}' cannot author a runtime Entity literal for '{parameter.ParameterId}'");
                        if (parameter.Literal.Kind == DecisionParameterKind.AuthoredId && !parameter.Literal.AuthoredId.IsSet)
                            errors.Add($"binding '{binding.BindingId}' has an unset AuthoredId literal for '{parameter.ParameterId}'");
                    }
                }
                foreach (KeyValuePair<AuthoredId, ConsiderationParameter> parameter in schema)
                {
                    if (parameter.Value.Required && !bound.Contains(parameter.Key))
                        errors.Add($"binding '{binding.BindingId}' leaves required parameter '{parameter.Key}' unbound");
                }

                var signals = new HashSet<AuthoredId>();
                for (int s = 0; s < binding.Signals.Count; s++)
                {
                    DecisionSignalRequest signal = binding.Signals[s];
                    if (!signal.SignalId.IsSet || !signals.Add(signal.SignalId))
                        errors.Add($"binding '{binding.BindingId}' has an unset or duplicate Signal '{signal.SignalId}'");
                    if (!providers.Contains(signal.ProviderId))
                        errors.Add($"binding '{binding.BindingId}' references unknown Signal provider '{signal.ProviderId}'");
                }
                for (int i = 0; i < binding.Field.LinearTerms.Count; i++)
                    RequireSignal(binding, binding.Field.LinearTerms[i].Signal, signals, errors);
                for (int i = 0; i < binding.Field.PairwiseTerms.Count; i++)
                {
                    RequireSignal(binding, binding.Field.PairwiseTerms[i].Pair.First, signals, errors);
                    RequireSignal(binding, binding.Field.PairwiseTerms[i].Pair.Second, signals, errors);
                }
                foreach (KeyValuePair<AuthoredId, long> ideal in binding.Field.IdealPoint)
                    RequireSignal(binding, ideal.Key, signals, errors);
                for (int f = 0; f < binding.Field.IdealFactors.Count; f++)
                    for (int c = 0; c < binding.Field.IdealFactors[f].Coefficients.Count; c++)
                        RequireSignal(binding, binding.Field.IdealFactors[f].Coefficients[c].Signal, signals, errors);

                long previousThreshold = -1;
                for (int t = 0; t < binding.Scale.Thresholds.Count; t++)
                {
                    ReasonDieThreshold threshold = binding.Scale.Thresholds[t];
                    if (threshold.MinimumMagnitude <= previousThreshold)
                        errors.Add($"binding '{binding.BindingId}' scale thresholds must be strictly increasing");
                    bool validDie = false;
                    for (int d = 0; d < Die.Ladder.Length; d++) validDie |= Die.Ladder[d] == threshold.Die.Sides;
                    if (!validDie) errors.Add($"binding '{binding.BindingId}' scale uses unsupported d{threshold.Die.Sides}");
                    previousThreshold = threshold.MinimumMagnitude;
                }

                bool appliesSomewhere = false;
                for (int o = 0; o < options.Count && !appliesSomewhere; o++)
                {
                    appliesSomewhere = OptionCanBind(binding, options[o]);
                }
                if (!appliesSomewhere)
                    errors.Add($"binding '{binding.BindingId}' cannot satisfy its required Option parameters on any Option");
            }
            return errors;
        }

        private static bool OptionCanBind(CompiledConsiderationBinding binding, DecisionOption option)
        {
            for (int p = 0; p < binding.ParameterSchema.Count; p++)
            {
                ConsiderationParameter declared = binding.ParameterSchema[p];
                if (!declared.Required) continue;
                for (int i = 0; i < binding.ParameterBindings.Count; i++)
                {
                    CompiledParameterBinding authored = binding.ParameterBindings[i];
                    if (authored.ParameterId == declared.Id && authored.Source == ParameterBindingSource.OptionContext &&
                        (!option.TryGetContext(authored.SourceParameterId, out DecisionParameterValue value) ||
                         value.Kind != declared.Kind)) return false;
                }
            }
            return true;
        }

        private static void RequireSignal(
            CompiledConsiderationBinding binding,
            AuthoredId signal,
            HashSet<AuthoredId> signals,
            List<string> errors)
        {
            if (!signals.Contains(signal))
                errors.Add($"binding '{binding.BindingId}' field references unrequested Signal '{signal}'");
        }
    }
}
