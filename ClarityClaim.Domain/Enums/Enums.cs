namespace ClarityClaim.Domain.Enums;

public enum ValidationSeverity { Critical = 3, Major = 2, Minor = 1 }

public enum ModelInstance { Qwen35B_120K, Qwen35B_240K, Gemma4_26B, Llama1B, Phi31Mini }

public enum SupportedLanguage { En, Es, Hi, Zh, Fr }

public enum PolicyType { LCD, NCD, BillingArticle }
