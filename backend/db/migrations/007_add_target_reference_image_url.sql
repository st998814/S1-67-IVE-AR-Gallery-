-- Optional real-world placement reference photo (distinct from Vuforia trackable image).

ALTER TABLE targets
    ADD COLUMN IF NOT EXISTS target_reference_image_url TEXT NOT NULL DEFAULT '';
