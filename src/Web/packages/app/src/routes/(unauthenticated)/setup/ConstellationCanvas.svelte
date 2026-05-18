<script lang="ts">
	let { progress = 0 }: { progress: number } = $props();

	let canvas: HTMLCanvasElement;
	let animProgress = 0;

	function rand(seed: number): number {
		const x = Math.sin(seed * 9301 + 49297) * 233280;
		return x - Math.floor(x);
	}

	function easeInOutCubic(t: number): number {
		return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
	}

	interface Star {
		scatteredX: number;
		scatteredY: number;
		gridX: number;
		gridY: number;
		radius: number;
		twinkleOffset: number;
	}

	const SPACING = 44;
	function generateStars(w: number, h: number): Star[] {
		const cols = Math.ceil(w / SPACING);
		const rows = Math.ceil(h / SPACING);
		const stars: Star[] = [];
		let seed = 0;

		for (let row = 0; row < rows; row++) {
			for (let col = 0; col < cols; col++) {
				const i = seed++;
				const gridX = col * SPACING + SPACING / 2;
				const gridY = row * SPACING + SPACING / 2;

				// Scatter locally around the grid position
				const angle = rand(i * 3) * Math.PI * 2;
				const scatter = SPACING * 0.6 + rand(i * 5) * SPACING * 1.4;
				const scatteredX = gridX + Math.cos(angle) * scatter + (rand(i * 11) - 0.5) * 20;
				const scatteredY = gridY + Math.sin(angle) * scatter + (rand(i * 13) - 0.5) * 20;

				stars.push({
					scatteredX,
					scatteredY,
					gridX,
					gridY,
					radius: 0.6 + rand(i * 17) * 1.4,
					twinkleOffset: rand(i * 19) * Math.PI * 2
				});
			}
		}

		return stars;
	}

	$effect(() => {
		if (!canvas) return;

		let stars: Star[] = [];
		let rafId: number;
		let time = 0;
		let lastW = 0;
		let lastH = 0;

		function syncSize() {
			const w = window.innerWidth;
			const h = window.innerHeight;
			if (w === lastW && h === lastH) return;
			lastW = w;
			lastH = h;
			canvas.width = w;
			canvas.height = h;
			stars = generateStars(w, h);
		}

		syncSize();
		window.addEventListener('resize', syncSize);

		function frame() {
			rafId = requestAnimationFrame(frame);
			time += 0.016;

			// Smoothly chase the progress prop
			animProgress += (progress - animProgress) * 0.05;

			const ctx = canvas.getContext('2d');
			if (!ctx) return;

			const w = canvas.width;
			const h = canvas.height;
			if (w === 0 || h === 0) return;
			ctx.clearRect(0, 0, w, h);

			const easedProgress = easeInOutCubic(Math.min(1, Math.max(0, animProgress)));

			// Draw connecting lines when progress > 0.5
			if (animProgress > 0.5) {
				const lineAlpha = (animProgress - 0.5) * 0.08;
				ctx.strokeStyle = `rgba(134, 239, 172, ${lineAlpha})`;
				ctx.lineWidth = 0.5;

				const cols = Math.ceil(w / SPACING);
				for (let i = 0; i < stars.length; i++) {
					// Connect to horizontal neighbor (next in same row)
					if ((i + 1) % cols !== 0 && i + 1 < stars.length) {
						const s1 = stars[i];
						const s2 = stars[i + 1];
						const x1 = s1.scatteredX + (s1.gridX - s1.scatteredX) * easedProgress;
						const y1 = s1.scatteredY + (s1.gridY - s1.scatteredY) * easedProgress;
						const x2 = s2.scatteredX + (s2.gridX - s2.scatteredX) * easedProgress;
						const y2 = s2.scatteredY + (s2.gridY - s2.scatteredY) * easedProgress;

						ctx.beginPath();
						ctx.moveTo(x1, y1);
						ctx.lineTo(x2, y2);
						ctx.stroke();
					}
				}
			}

			// Draw stars
			for (const star of stars) {
				const drift = (1 - easedProgress) * 1.5;
				const driftX = Math.sin(time * 0.15 + star.twinkleOffset) * drift;
				const driftY = Math.cos(time * 0.12 + star.twinkleOffset * 1.3) * drift;
				const x = star.scatteredX + (star.gridX - star.scatteredX) * easedProgress + driftX;
				const y = star.scatteredY + (star.gridY - star.scatteredY) * easedProgress + driftY;

				// Distance fade from center
				const dx = x / w - 0.5;
				const dy = y / h - 0.5;
				const distFade = 1 - Math.sqrt(dx * dx + dy * dy) * 0.8;

				// Twinkle
				const twinkle = 0.7 + 0.3 * Math.sin(time * 0.4 + star.twinkleOffset);

				const alpha = 0.5 * distFade * twinkle + animProgress * 0.3 * distFade;

				ctx.fillStyle = `rgba(134, 239, 172, ${Math.max(0, alpha)})`;
				ctx.beginPath();
				ctx.arc(x, y, star.radius, 0, Math.PI * 2);
				ctx.fill();
			}
		}

		rafId = requestAnimationFrame(frame);

		return () => {
			cancelAnimationFrame(rafId);
			window.removeEventListener('resize', syncSize);
		};
	});
</script>

<canvas bind:this={canvas} class="pointer-events-none absolute inset-0 h-full w-full"></canvas>
