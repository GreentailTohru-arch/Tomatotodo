import 'package:flutter/material.dart';

class WorkspacePage extends StatelessWidget {
  const WorkspacePage({
    super.key,
    required this.title,
    required this.children,
    this.actions,
  });
  final String title;
  final List<Widget> children;
  final List<Widget>? actions;
  @override
  Widget build(BuildContext context) => CustomScrollView(
    slivers: [
      SliverAppBar(title: Text(title), pinned: true, actions: actions),
      SliverToBoxAdapter(
        child: SafeArea(
          top: false,
          child: Center(
            child: ConstrainedBox(
              constraints: BoxConstraints(maxWidth: 720),
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: children,
                ),
              ),
            ),
          ),
        ),
      ),
    ],
  );
}

class EmptyState extends StatelessWidget {
  const EmptyState({
    super.key,
    required this.icon,
    required this.title,
    required this.description,
  });
  final IconData icon;
  final String title;
  final String description;
  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card.outlined(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 40),
        child: Column(
          children: [
            Icon(icon, size: 48, color: theme.colorScheme.primary),
            SizedBox(height: 20),
            Text(
              title,
              style: theme.textTheme.titleLarge,
              textAlign: TextAlign.center,
            ),
            SizedBox(height: 8),
            Text(
              description,
              textAlign: TextAlign.center,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
