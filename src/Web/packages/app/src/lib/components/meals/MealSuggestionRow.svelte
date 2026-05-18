<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { Sparkles } from "lucide-svelte";
  import type { SuggestedMealMatch } from "$lib/api";

  let {
    suggestion,
    onAccept,
    onDismiss,
    onReview,
  }: {
    suggestion: SuggestedMealMatch;
    onAccept: (suggestion: SuggestedMealMatch) => void;
    onDismiss: (suggestion: SuggestedMealMatch) => void;
    onReview: (suggestion: SuggestedMealMatch) => void;
  } = $props();
</script>

<div class="flex items-center justify-between gap-4">
  <div class="flex items-center gap-3 min-w-0">
    <Sparkles class="h-4 w-4 text-primary shrink-0" />
    <div class="min-w-0">
      <span class="font-medium truncate">
        {suggestion.foodName ?? suggestion.mealName ?? "Food entry"}
      </span>
      <span class="text-sm text-muted-foreground ml-2">
        {suggestion.carbs}g carbs
        · {Math.round((suggestion.matchScore ?? 0) * 100)}% match
      </span>
    </div>
  </div>
  <div class="flex items-center gap-2 shrink-0">
    <Button
      type="button"
      variant="ghost"
      size="sm"
      onclick={(e: MouseEvent) => {
        e.stopPropagation();
        onDismiss(suggestion);
      }}
    >
      Dismiss
    </Button>
    <Button
      type="button"
      variant="outline"
      size="sm"
      onclick={(e: MouseEvent) => {
        e.stopPropagation();
        onReview(suggestion);
      }}
    >
      Review
    </Button>
    <Button
      type="button"
      size="sm"
      onclick={(e: MouseEvent) => {
        e.stopPropagation();
        onAccept(suggestion);
      }}
    >
      Accept
    </Button>
  </div>
</div>
